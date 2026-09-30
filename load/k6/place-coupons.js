// Places singles and accumulators through the gateway. Target: 150 placements/s sustained, p99 < 250 ms.
import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://127.0.0.1:7100/api';
const PASSWORD = __ENV.DEMO_PASSWORD;
const RATE = Number(__ENV.RATE || 150);

export const options = {
  scenarios: {
    placement: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 400,
      stages: [
        { target: RATE, duration: __ENV.RAMP || '1m' },
        { target: RATE, duration: __ENV.HOLD || '5m' },
      ],
    },
  },
  thresholds: {
    'http_req_duration{name:place}': ['p(99)<250'],
    'checks{name:place}': ['rate>0.95'],
  },
};

const PUNTERS = Number(__ENV.PUNTERS || 100);
const json = { 'Content-Type': 'application/json' };

// Funds each load punter (the top-up opens their wallet account) and signs them in.
export function setup() {
  const operator = http.post(`${BASE}/auth/token`, JSON.stringify({ grantType: 'password', username: 'operator1', password: PASSWORD }), { headers: json }).json('accessToken');
  const tokens = [];
  for (let i = 1; i <= PUNTERS; i++) {
    const id = `40000000-0000-0000-0000-${String(i).padStart(12, '0')}`;
    http.post(`${BASE}/accounts/${id}/topup`, JSON.stringify({ minorUnits: 10000000, currency: 'ZAR' }), {
      headers: { ...json, Authorization: `Bearer ${operator}`, 'Idempotency-Key': `k6-fund-${id}-${Date.now()}` },
    });
    tokens.push(http.post(`${BASE}/auth/token`, JSON.stringify({ grantType: 'password', username: `load${String(i).padStart(3, '0')}`, password: PASSWORD }), { headers: json }).json('accessToken'));
  }
  return { tokens };
}

export default function (data) {
  const token = data.tokens[exec.scenario.iterationInTest % data.tokens.length];
  const fixtures = http.get(`${BASE}/fixtures/?limit=20`, { tags: { name: 'fixtures' } }).json();
  if (!Array.isArray(fixtures) || fixtures.length === 0) {
    return;
  }

  const legCount = Math.random() < 0.5 ? 1 : Math.min(fixtures.length, 2 + Math.floor(Math.random() * 3));
  const legs = shuffle(fixtures).slice(0, legCount).map((f) => {
    const market = f.markets[Math.floor(Math.random() * f.markets.length)];
    const selection = market.selections[Math.floor(Math.random() * market.selections.length)];
    return { fixtureId: f.fixtureId, marketId: market.marketId, selectionId: selection.selectionId, odds: selection.odds, offerVersion: f.offerVersion };
  });

  const res = http.post(`${BASE}/coupons/`, JSON.stringify({ stake: 100, currency: 'ZAR', legs }), {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, 'Idempotency-Key': `k6-${exec.scenario.iterationInTest}-${Date.now()}` },
    tags: { name: 'place' },
  });
  // A price that moved or a closed market is a correct refusal, not an error.
  check(res, { 'placed or correctly refused': (r) => [201, 409, 422].includes(r.status) }, { name: 'place' });
  check(res, { placed: (r) => r.status === 201 }, { name: 'placed' });
}

function shuffle(items) {
  return items.map((v) => [Math.random(), v]).sort((a, b) => a[0] - b[0]).map((p) => p[1]);
}

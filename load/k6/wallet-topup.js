// Operator top-ups against the wallet's HTTP surface, each with a fresh idempotency key.
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://127.0.0.1:7100/api';

export const options = { vus: 5, duration: __ENV.DURATION || '30s', thresholds: { http_req_duration: ['p(99)<250'] } };

export function setup() {
  const res = http.post(`${BASE}/auth/token`, JSON.stringify({ grantType: 'password', username: 'operator1', password: __ENV.DEMO_PASSWORD }), {
    headers: { 'Content-Type': 'application/json' },
  });
  return { token: res.json('accessToken') };
}

export default function (data) {
  const account = `10000000-0000-0000-0000-00000000000${1 + Math.floor(Math.random() * 5)}`;
  const res = http.post(`${BASE}/accounts/${account}/topup`, JSON.stringify({ minorUnits: 10000, currency: 'ZAR' }), {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${data.token}`, 'Idempotency-Key': `k6-topup-${__VU}-${__ITER}-${Date.now()}` },
  });
  check(res, { 'topped up': (r) => r.status === 200 });
}

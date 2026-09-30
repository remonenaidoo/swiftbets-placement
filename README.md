# swiftbets-placement

[![ci](https://github.com/remonenaidoo/swiftbets-placement/actions/workflows/ci.yml/badge.svg)](https://github.com/remonenaidoo/swiftbets-placement/actions/workflows/ci.yml)

Bet placement for SwiftBets: identity (RS256 JWT + JWKS), coupon validation, the price-change policy, the risk module, and the placement saga with durable intents, an orphan sweeper, a transactional outbox and a claiming relay. The wallet ledger lives here as a separate process with its own database and login, reached over gRPC.

## Hosts

- `SwiftBets.Placement.Api`: identity and coupon placement.
- `SwiftBets.Wallet.Api`: double-entry wallet ledger; gRPC `swiftbets.wallet.v1.Wallet` for placement and payout, HTTP only for operator top-up.
- `SwiftBets.Placement.Migrator`, `SwiftBets.Wallet.Migrator`: one-shot DbUp migrators.

## Data and events

- **Owns:** SQL Server `SbPlacement` (coupons, legs, saga intents, outbox, inbox) and `SbWallet` (accounts, ledger entries, reservations, idempotency), each with its own least-privilege login.
- **Events:** Produces `placement.coupon-placed`, `placement.coupon-rejected`, `wallet.ledger-posted`.

## Layout

Clean Architecture, enforced by project references and `*.ArchitectureTests`:

```
src/*.Domain          pure domain, no references
src/*.Application     use cases and ports; depends on Domain and contracts only
src/*.Infrastructure  adapters (Dapper + embedded .sql, Kafka, Redis); implements Application ports
src/*.Api | *.Worker  composition root: observability, error envelope, health, metrics
src/*.Migrator        DbUp scripts under Migrations/, run once before the host starts
```

Every host exposes `/health/live`, `/health/ready` (checks its real dependencies), `/metrics` (Prometheus), logs compact JSON with correlation ids, and exports traces over OTLP.

## Build and test

```bash
../swiftbets-platform/scripts/fetch-shared-packages.sh .   # or pack-local.sh for unreleased shared changes
dotnet test SwiftBets.Placement.slnx
```

Integration tests use Testcontainers and need Docker. The whole platform runs from `swiftbets-platform` with `make up`.

## Images

Multi-arch (amd64 + arm64), non-root, chiseled runtime:

- `ghcr.io/remonenaidoo/swiftbets-placement`
- `ghcr.io/remonenaidoo/swiftbets-placement-migrator`
- `ghcr.io/remonenaidoo/swiftbets-wallet`
- `ghcr.io/remonenaidoo/swiftbets-wallet-migrator`

## License

MIT

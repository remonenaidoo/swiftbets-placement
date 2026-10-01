# swiftbets-placement

[![ci](https://github.com/remonenaidoo/swiftbets-placement/actions/workflows/ci.yml/badge.svg)](https://github.com/remonenaidoo/swiftbets-placement/actions/workflows/ci.yml)

Bet placement for SwiftBets: coupon validation, the price-change policy, the risk module, the placement saga with durable intents, an orphan sweeper, a transactional outbox and a claiming relay, and the bet-history projection. The wallet ([swiftbets-wallet](https://github.com/remonenaidoo/swiftbets-wallet)) is called over gRPC; tokens come from [swiftbets-identity](https://github.com/remonenaidoo/swiftbets-identity). Placement's legacy identity module stays read-only for one release after the cut-over, then goes.

## Hosts

- `SwiftBets.Placement.Api`: coupon placement, my bets, and the legacy identity module.
- `SwiftBets.Placement.Migrator`, `SwiftBets.History.Migrator`: one-shot DbUp migrators.

## Data and events

- **Owns:** SQL Server `SbPlacement` (coupons, legs, saga intents, outbox, inbox) and Postgres `sb_history` (the my-bets projection), each with its own least-privilege login.
- **Events:** Produces `placement.coupon-placed`, `placement.coupon-rejected`.

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
- `ghcr.io/remonenaidoo/swiftbets-history-migrator`

## License

MIT

# AGENTS.md

.NET 10 solution using the `.slnx` format (`JobQueue.slnx`). Clean Architecture: `Domain` (zero deps) <- `Application` <- `Infrastructure` / `Api` / `Worker`.

## Commands

- `dotnet test` — all tests (needs no infra; IntegrationTests/ApiTests are currently in-memory/placeholder).
- `dotnet test tests/JobQueue.UnitTests` — focused suite; substitute other test project paths.
- `dotnet run --project src/JobQueue.Api` / `dotnet run --project src/JobQueue.Worker` — run individually.
- Infra for local dev: `docker compose up -d sqlserver rabbitmq` (API `http://localhost:5208`, RabbitMQ mgmt `http://localhost:15672` guest/guest).
- Full stack: `docker compose up -d`; scale with `docker compose up -d --scale worker=3`.
- Apply migrations (run from `src/JobQueue.Api`): `dotnet ef database update --project ../JobQueue.Infrastructure`. Migrations live in Infrastructure; the context/migrations assembly is Infrastructure, startup project is Api. Local `appsettings.json` points at `DESKTOP-IO3AQMJ\SQLEXPRESS` — override via `ConnectionStrings__JobQueue` env in compose. `Database:ApplyMigrationsOnStartup=true` is set only in compose; never rely on it locally.

## Architecture (non-obvious)

- No MediatR. Application command/query handlers are concrete `scoped` classes (`AddApplication()` in `src/JobQueue.Application/DependencyInjection.cs:19`); controllers inject them directly.
- `Api` registers Infrastructure with **no** consumer assemblies — it only publishes. `Worker` passes its own assembly, owns `ProcessJobConsumer`, `IJobHandler`s, and the `StuckJobRecoveryService` + `ScheduledJobDispatcherService` background sweeps (`src/JobQueue.Worker/Program.cs`).
- Messages carry only `JobId` (+ type/correlationId); workers always load authoritative state from SQL Server.
- New job type: add an `IJobHandler` in `src/JobQueue.Worker/Jobs/` (auto-discovered by `AddJobHandlers`) and use the exact string from `JobTypes` (`src/JobQueue.Domain/Jobs/JobTypes.cs`) as `Job.Type`. Payload test knobs: `"failAttempts": N` throws transient `TimeoutException`; `"workSeconds": S` + `"slowAttempts": N` simulates slow runs.
- Transactional outbox (MassTransit EF outbox): in `CreateJobCommandHandler`, **publish BEFORE `SaveChangesAsync`** so job row + outbox message commit atomically. Only `Pending` jobs publish at creation; `Scheduled` jobs are published later by the dispatcher. Never "fix" this ordering.
- Health split: `/health` is an always-healthy liveness probe (`Predicate = _ => false`); `/health/ready` checks SQL Server + RabbitMQ. Worker serves these plus `/metrics` on `Worker:MetricsPort` (default 5209) via `MetricsEndpointService`.

## Invariants (do not break)

- Never assign `Job.Status` directly — always `job.TransitionTo(...)`, enforced by `JobStatusTransitions` (`src/JobQueue.Domain/Jobs/JobStatusTransitions.cs`). `Completed`/`Cancelled` are terminal. Retry only from `Failed`/`DeadLettered` -> `Pending`.
- Optimistic concurrency via SQL `rowversion`: on `DbUpdateConcurrencyException` the loser defers (consumer's `TrySaveClaimAsync`; cancel endpoint retries once then returns 409). After `heartbeat.StopAsync()`, always `RefreshAsync(job)` and re-check `Status`/`WorkerId` before writing an outcome — never overwrite a row you no longer own.
- Error classification (`JobErrorClassifier`): `PermanentJobException`, `ArgumentException`, `KeyNotFoundException` -> permanent (`Failed`, no retry); `TimeoutException`, `IOException`, `HttpRequestException`, unknown -> transient (retry); Infrastructure adds `DbUpdateException`/`DbUpdateConcurrencyException` as transient. Consumer rethrows transient while `Attempts < MaxAttempts`, dead-letters + rethrows when exhausted, fails immediately on permanent.
- Retry policy is fixed in `ProcessJobConsumerDefinition`: exponential 2s -> 4s -> 8s cap (`RetryCount = 2`, i.e. 3 attempts total), `Ignore<OperationCanceledException>`. `Worker:HeartbeatTimeoutSeconds` must stay larger than the max 8s retry delay or recovery will steal mid-retry jobs. Note `appsettings.json` (2/10/2/2s) differs from `WorkerOptions.cs` code defaults (10/30/10/5s) — the json wins locally.
- Idempotent create: `Idempotency-Key` header has a filtered unique index; replays return `202` with `Idempotent-Replay: true` and do not publish or count metrics. `X-Correlation-Id` is resolved by `CorrelationIdMiddleware`, echoed in the response, and flows job record -> message -> handler logs.

# JobQueueSystem

A production-grade **background job processing system** built with ASP.NET Core 10, demonstrating distributed-systems patterns including message queuing, retry with exponential backoff, dead-letter queues, idempotency, optimistic concurrency, and the transactional outbox pattern.

Inspired by architectures like Hangfire and RabbitMQ worker systems, this project exposes an HTTP API for creating and managing jobs while background workers consume and process them asynchronously via RabbitMQ.

---

## Table of Contents

- [Technology Stack](#technology-stack)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Features](#features)
- [API Endpoints](#api-endpoints)
- [Job Types](#job-types)
- [Job Lifecycle](#job-lifecycle)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Docker Compose](#docker-compose)
- [Testing](#testing)
- [Observability](#observability)
- [Key Design Decisions](#key-design-decisions)

---

## Technology Stack

| Technology | Purpose |
|---|---|
| **ASP.NET Core 10** | Web API framework |
| **C#** | Implementation language |
| **Entity Framework Core 10** | ORM / data access |
| **SQL Server 2022** | Job persistence & concurrency control |
| **RabbitMQ 3.13** | Message broker / transport |
| **MassTransit 8.5** | Message bus abstraction (consumers, retry, outbox) |
| **ASP.NET Core BackgroundService** | Scheduled dispatch & stuck-job recovery |
| **Prometheus (prometheus-net)** | Metrics & observability |
| **Scalar** | Interactive OpenAPI documentation |
| **Docker / Docker Compose** | Containerized development environment |
| **xUnit** | Unit & integration testing |

---

## Architecture

```text
                         ┌─────────────────┐
                         │     Client      │
                         └────────┬────────┘
                                  │ HTTP
                                  ▼
                         ┌─────────────────┐
                         │ ASP.NET Core    │
                         │      API        │
                         └────────┬────────┘
                                  │
                            SQL Transaction
                                  │
                         ┌────────┴────────┐
                         │                 │
                         ▼                 ▼
                    ┌─────────┐      ┌──────────┐
                    │  Jobs   │      │  Outbox  │
                    │   DB    │      │ Messages │
                    └─────────┘      └────┬─────┘
                                          │
                                          ▼
                                   ┌───────────┐
                                   │ RabbitMQ  │
                                   └─────┬─────┘
                                         │
                    ┌────────────────────┼────────────────────┐
                    │                    │                    │
                    ▼                    ▼                    ▼
              ┌──────────┐        ┌──────────┐        ┌──────────┐
              │ Worker 1 │        │ Worker 2 │        │ Worker 3 │
              └────┬─────┘        └────┬─────┘        └────┬─────┘
                   │                   │                   │
                   └───────────────────┼───────────────────┘
                                       │
                                       ▼
                               ┌───────────────┐
                               │ Job Handlers  │
                               ├───────────────┤
                               │ SendEmail     │
                               │ Invoice       │
                               │ Inventory     │
                               │ Notification  │
                               └───────────────┘
```

The system follows **Clean Architecture** principles:

```text
Domain  ←  Application  ←  Infrastructure / API / Worker
```

- **Domain** has no dependencies on frameworks, databases, or message brokers.
- **Application** contains commands, queries, and abstractions.
- **Infrastructure** implements persistence (EF Core), messaging (MassTransit), and health checks.
- **API** exposes HTTP endpoints for job management.
- **Worker** consumes messages and executes job handlers.

---

## Project Structure

```text
JobQueueSystem/
├── src/
│   ├── JobQueue.Api/                  # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   ├── Contracts/
│   │   ├── Middleware/
│   │   ├── Observability/
│   │   ├── Program.cs
│   │   ├── Dockerfile
│   │   └── appsettings.json
│   │
│   ├── JobQueue.Application/          # Application layer (CQRS handlers)
│   │   ├── Abstractions/
│   │   ├── Jobs/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   └── Services/
│   │   └── DependencyInjection.cs
│   │
│   ├── JobQueue.Domain/               # Domain model (zero dependencies)
│   │   ├── Enums/
│   │   ├── Events/
│   │   ├── Exceptions/
│   │   └── Jobs/
│   │
│   ├── JobQueue.Infrastructure/       # Persistence, messaging, health
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   ├── Messaging/
│   │   ├── Health/
│   │   └── DependencyInjection.cs
│   │
│   └── JobQueue.Worker/               # Background worker service
│       ├── Consumers/
│       ├── Jobs/                      # IJobHandler implementations
│       ├── BackgroundServices/
│       ├── Observability/
│       ├── Services/
│       ├── Program.cs
│       ├── Dockerfile
│       └── appsettings.json
│
├── tests/
│   ├── JobQueue.UnitTests/
│   ├── JobQueue.IntegrationTests/
│   └── JobQueue.ApiTests/
│
├── docker-compose.yml
├── PLAN.md
└── JobQueue.slnx
```

---

## Features

### Core Job Processing
- **Asynchronous job execution** — the API enqueues work and returns `202 Accepted` immediately; workers process jobs in the background.
- **Multiple job handlers** resolved by job type via a handler abstraction (`IJobHandler`).
- **Worker concurrency** — configurable concurrent message processing per worker instance.

### Reliability
- **Retry with exponential backoff** — transient failures are retried (2s → 4s → 8s) using MassTransit's retry pipeline.
- **Dead-letter queue** — jobs exceeding `MaxAttempts` are dead-lettered; the message moves to RabbitMQ's `_error` queue.
- **Error classification** — transient errors (timeouts, DB failures) are retried; permanent errors (invalid payload) fail immediately.
- **Stuck-job recovery** — a heartbeat mechanism detects crashed workers and re-dispatches orphaned jobs.

### Scheduling & Lifecycle
- **Scheduled jobs** — jobs can be created with a future `scheduledAt` time; a dispatcher background service picks them up when due.
- **Job cancellation** — cooperative cancellation via `CancellationToken`; pending/scheduled jobs cancel immediately, processing jobs cancel when the handler checks the token.
- **Manual retry** — operators can re-enqueue failed or dead-lettered jobs.

### Consistency & Idempotency
- **Transactional outbox** — messages are written to an outbox table in the same SQL transaction as job state changes, eliminating the dual-write problem.
- **Idempotent job creation** — an `Idempotency-Key` header prevents duplicate jobs from repeated requests.
- **Optimistic concurrency** — SQL Server `rowversion` prevents concurrent workers from overwriting each other's state.
- **Centralized state machine** — all status transitions are validated through `JobStatusTransitions`; invalid transitions throw.

### Observability
- **Prometheus metrics** — job creation, processing, attempts, duration, recovery, retries, cancellations.
- **Structured logging** — correlation IDs propagated across HTTP → message → consumer → handler.
- **Health checks** — `/health` (liveness) and `/health/ready` (SQL Server + RabbitMQ dependency checks).

### Operations
- **Graceful shutdown** — workers drain in-flight jobs within a configurable timeout on SIGTERM.
- **Horizontal scaling** — run multiple worker instances consuming from the same queue.
- **Execution history** — every attempt is recorded in a `JobAttempts` table.

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/jobs` | Create a new job (immediate or scheduled) |
| `GET` | `/jobs/{id}` | Get a single job by ID |
| `GET` | `/jobs` | List jobs with filtering & pagination |
| `GET` | `/jobs/failed` | List failed jobs |
| `GET` | `/jobs/{id}/attempts` | Get execution history for a job |
| `POST` | `/jobs/{id}/retry` | Retry a failed/dead-lettered job |
| `POST` | `/jobs/{id}/cancel` | Cancel a pending/scheduled/processing job |
| `GET` | `/health` | Liveness probe |
| `GET` | `/health/ready` | Readiness probe (SQL Server + RabbitMQ) |
| `GET` | `/metrics` | Prometheus metrics |
| `GET` | `/scalar/v1` | Interactive API documentation |

### Example: Create a Job

```http
POST /jobs
Idempotency-Key: order-123-email
X-Correlation-Id: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{
  "type": "SendEmail",
  "payload": {
    "to": "customer@example.com",
    "subject": "Order Confirmation"
  },
  "priority": 5,
  "maxAttempts": 3
}
```

**Response:** `202 Accepted`
```json
{
  "id": "8f1a2b3c-...",
  "status": "Pending"
}
```

### Example: Create a Scheduled Job

```http
POST /jobs
Content-Type: application/json

{
  "type": "GenerateInvoice",
  "payload": { "orderId": "123" },
  "scheduledAt": "2026-09-15T18:00:00Z"
}
```

**Response:** `202 Accepted`
```json
{
  "id": "8f1a2b3c-...",
  "status": "Scheduled"
}
```

### Example: List Jobs

```http
GET /jobs?status=Failed&type=SendEmail&page=1&pageSize=20
```

---

## Job Types

| Type | Handler | Description |
|------|---------|-------------|
| `SendEmail` | `SendEmailJobHandler` | Simulates sending an email |
| `GenerateInvoice` | `GenerateInvoiceJobHandler` | Simulates CPU/file work |
| `UpdateInventory` | `UpdateInventoryJobHandler` | Simulates database work |
| `NotifyUser` | `NotifyUserJobHandler` | Simulates an external HTTP call |

Handlers support **failure simulation** for testing retry/backoff behavior via payload properties:
- `"failAttempts": N` — the first N executions throw a transient `TimeoutException`
- `"workSeconds": S` with `"slowAttempts": N` — the first N executions take S seconds

---

## Job Lifecycle

```text
                    ┌───────────┐
                    │ Scheduled │
                    └─────┬─────┘
                          │ (dispatcher)
                          ▼
                    ┌───────────┐
              ┌────►│  Pending  │
              │     └─────┬─────┘
              │           │ (worker claim)
              │           ▼
              │     ┌────────────┐
              │     │ Processing │
              │     └─────┬──────┘
              │           │
          (retry)         ├──────────────┐
              │           │              │
              │           ▼              ▼
              │     ┌───────────┐  ┌──────────┐
              │     │ Completed │  │  Failed  │
              │     └───────────┘  └────┬─────┘
              │                         │
              │                  (max attempts)
              │                         │
              │                         ▼
              │                  ┌─────────────┐
              └──────────────────│DeadLettered │
                                 └─────────────┘

  Pending / Scheduled ──► Cancelled
```

**Valid transitions are enforced by `JobStatusTransitions`.** Any code attempting an invalid transition throws `InvalidJobTransitionException`.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- SQL Server (local or Docker)
- RabbitMQ (local or Docker)

### Option 1: Docker Compose (Recommended)

```bash
# Start all infrastructure + application
docker compose up -d

# Scale workers to 3 instances
docker compose up -d --scale worker=3
```

Services:
| Service | URL |
|---------|-----|
| API | http://localhost:5208 |
| API Documentation (Scalar) | http://localhost:5208/scalar/v1 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| SQL Server | localhost:1433 |

### Option 2: Local Development

1. **Start infrastructure:**
   ```bash
   docker compose up -d sqlserver rabbitmq
   ```

2. **Update connection strings** in `appsettings.json` (API and Worker) to match your local SQL Server.

3. **Apply migrations:**
   ```bash
   cd src/JobQueue.Api
   dotnet ef database update --project ../JobQueue.Infrastructure
   ```

4. **Run the API:**
   ```bash
   cd src/JobQueue.Api
   dotnet run
   ```

5. **Run a worker:**
   ```bash
   cd src/JobQueue.Worker
   dotnet run
   ```

### Submit Your First Job

```bash
curl -X POST http://localhost:5208/jobs \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: my-first-job" \
  -d '{
    "type": "SendEmail",
    "payload": {
      "to": "hello@example.com",
      "subject": "Hello from JobQueue!"
    }
  }'
```

---

## Configuration

### API (`appsettings.json`)

| Setting | Description |
|---------|-------------|
| `ConnectionStrings:JobQueue` | SQL Server connection string |
| `RabbitMq:Host` | RabbitMQ hostname |
| `RabbitMq:Username` | RabbitMQ username |
| `RabbitMq:Password` | RabbitMQ password |
| `Database:ApplyMigrationsOnStartup` | Auto-apply EF migrations (Docker) |

### Worker (`appsettings.json`)

| Setting | Default | Description |
|---------|---------|-------------|
| `Worker:Concurrency` | `5` | Concurrent messages per worker |
| `Worker:HeartbeatIntervalSeconds` | `2` | Heartbeat refresh interval |
| `Worker:HeartbeatTimeoutSeconds` | `10` | Stuck-job detection threshold |
| `Worker:RecoveryIntervalSeconds` | `2` | Stuck-job recovery sweep interval |
| `Worker:DispatchIntervalSeconds` | `2` | Scheduled-job dispatch sweep interval |
| `Worker:ShutdownTimeoutSeconds` | `60` | Graceful shutdown window |
| `Worker:MetricsPort` | `5209` | Prometheus metrics endpoint port |

---

## Docker Compose

The `docker-compose.yml` provisions:

| Service | Image | Ports |
|---------|-------|-------|
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | 1433 |
| `rabbitmq` | `rabbitmq:3.13-management` | 5672, 15672 |
| `api` | Custom (Dockerfile) | 5208 → 8080 |
| `worker` | Custom (Dockerfile) | — |

Health checks ensure the API and Worker start only after SQL Server and RabbitMQ are ready.

```bash
# Full stack
docker compose up -d

# Scale workers
docker compose up -d --scale worker=3

# View logs
docker compose logs -f worker

# Tear down
docker compose down -v
```

---

## Testing

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/JobQueue.UnitTests

# Run integration tests only
dotnet test tests/JobQueue.IntegrationTests

# Run API tests only
dotnet test tests/JobQueue.ApiTests
```

### Test Coverage

| Project | Focus |
|---------|-------|
| `JobQueue.UnitTests` | State transitions, error classification, handler resolution, command handlers |
| `JobQueue.IntegrationTests` | DB-aware error classification, infrastructure behavior |
| `JobQueue.ApiTests` | API endpoint behavior |

---

## Observability

### Prometheus Metrics

**API metrics** (exposed at `/metrics`):
| Metric | Description |
|--------|-------------|
| `jobqueue_jobs_created_total` | Jobs created (by type) |
| `jobqueue_jobs_retried_total` | Manual retry requests |
| `jobqueue_jobs_cancelled_total` | Cancellation requests |

**Worker metrics** (exposed at `http://localhost:5209/metrics`):
| Metric | Description |
|--------|-------------|
| `jobqueue_jobs_processed_total` | Terminal outcomes (by type & result) |
| `jobqueue_job_attempts_total` | Processing attempts started (by type) |
| `jobqueue_job_duration_seconds` | Duration of successful executions |
| `jobqueue_jobs_recovered_total` | Stuck jobs recovered |
| `jobqueue_scheduled_dispatched_total` | Scheduled jobs dispatched |

### Correlation & Structured Logging

Every request receives a correlation ID (`X-Correlation-Id`) that propagates through:

```text
HTTP Request → Job Record → RabbitMQ Message → Consumer → Handler
```

All log entries within a job execution include `JobId`, `CorrelationId`, `AttemptNumber`, and `WorkerId` via structured logging scopes.

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Message carries only JobId** | Workers load authoritative state from SQL Server; avoids stale data in messages |
| **Transactional outbox** | Eliminates the dual-write problem between SQL commit and broker publish |
| **Optimistic concurrency (rowversion)** | Multiple workers can safely compete for the same job without locks |
| **Centralized state machine** | Prevents invalid status transitions from any code path |
| **Heartbeat-based stuck detection** | Recovers jobs from crashed workers without distributed locks |
| **Handler abstraction** | Queue infrastructure is independent from business logic |
| **Cooperative cancellation** | Running jobs check `CancellationToken`; safe and predictable |
| **At-least-once delivery + idempotency** | Embraces RabbitMQ's delivery semantics; handlers must tolerate duplicates |

---

## License

This project is a learning/educational project.

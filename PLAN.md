# Job Queue System — ASP.NET Core 10

## 1. Project Overview

Build a simplified background job processing system similar in concept to **Hangfire/RabbitMQ worker architectures**.

The system will expose an HTTP API for creating and managing jobs while background workers consume and process those jobs asynchronously.

### Technology Stack

* **ASP.NET Core 10**
* **C#**
* **Entity Framework Core 10**
* **SQL Server**
* **RabbitMQ**
* **MassTransit**
* **ASP.NET Core `BackgroundService`**
* **Docker / Docker Compose**
* **xUnit** for testing

### High-Level Architecture

```text
                         ┌──────────────────┐
                         │     Client       │
                         └────────┬─────────┘
                                  │
                                  │ HTTP
                                  ▼
                         ┌──────────────────┐
                         │   ASP.NET Core   │
                         │       API        │
                         └───────┬──────────┘
                                 │
                 ┌───────────────┼────────────────┐
                 │               │                │
                 ▼               ▼                ▼
            SQL Server       RabbitMQ        Job Scheduler
            Job State        Message Bus      / Dispatcher
                 │               │                │
                 │               ▼                │
                 │       ┌───────────────┐        │
                 │       │ MassTransit   │        │
                 │       │ Consumers     │        │
                 │       └───────┬───────┘        │
                 │               │                │
                 │       ┌───────┼───────┐        │
                 │       ▼       ▼       ▼        │
                 │    Worker  Worker  Worker      │
                 │       1       2       3         │
                 │               │                │
                 └───────────────┴────────────────┘
                                 │
                                 ▼
                         Job status / result
```

---

# 2. Learning Objectives

By completing this project, you should understand:

1. Asynchronous request processing.
2. Message queues.
3. Producer/consumer architecture.
4. RabbitMQ exchanges, queues, acknowledgements, and routing.
5. MassTransit consumers.
6. Background workers.
7. Retry strategies.
8. Exponential backoff.
9. Dead-letter queues.
10. Scheduled jobs.
11. Job cancellation.
12. Idempotency.
13. Worker concurrency.
14. At-least-once message delivery.
15. Database/message consistency problems.
16. Job state machines.
17. Observability and structured logging.
18. Graceful worker shutdown.
19. Horizontal scaling.
20. Failure recovery.

---

# 3. Solution Structure

Start with a modular solution.

```text
JobQueue/
│
├── src/
│   │
│   ├── JobQueue.Api/
│   │   ├── Controllers/
│   │   ├── Contracts/
│   │   ├── Extensions/
│   │   ├── Middleware/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── JobQueue.Application/
│   │   ├── Jobs/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   └── Services/
│   │   ├── Abstractions/
│   │   └── DependencyInjection.cs
│   │
│   ├── JobQueue.Domain/
│   │   ├── Jobs/
│   │   ├── Enums/
│   │   ├── Events/
│   │   └── Exceptions/
│   │
│   ├── JobQueue.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── JobQueueDbContext.cs
│   │   │   ├── Configurations/
│   │   │   └── Migrations/
│   │   ├── Messaging/
│   │   ├── Jobs/
│   │   └── DependencyInjection.cs
│   │
│   └── JobQueue.Worker/
│       ├── Consumers/
│       ├── Jobs/
│       ├── BackgroundServices/
│       └── Program.cs
│
└── tests/
    ├── JobQueue.UnitTests/
    ├── JobQueue.IntegrationTests/
    └── JobQueue.ApiTests/
```

You can initially keep API and Worker in the same ASP.NET Core application if you want a simpler first version, but separating them is preferable for learning horizontal scaling.

---

# 4. Phase 1 — Create the Solution

Create:

```text
JobQueue.Api
JobQueue.Application
JobQueue.Domain
JobQueue.Infrastructure
JobQueue.Worker
```

References:

```text
Api
 ├── Application
 └── Infrastructure

Worker
 ├── Application
 └── Infrastructure

Infrastructure
 ├── Application
 └── Domain

Application
 └── Domain

Domain
 └── nothing
```

The important rule is:

```text
Domain
   ↑
Application
   ↑
Infrastructure / API / Worker
```

The domain should not know about:

* SQL Server
* RabbitMQ
* MassTransit
* ASP.NET Core

---

# 5. Phase 2 — Define the Job Domain Model

Start with the job entity.

## Job

Suggested fields:

```text
Job
--------------------------------
Id
Type
Payload
Status
Priority
Attempts
MaxAttempts
CreatedAt
ScheduledAt
StartedAt
CompletedAt
FailedAt
CancelledAt
LastError
IdempotencyKey
CorrelationId
WorkerId
```

### Job Status

Create:

```csharp
public enum JobStatus
{
    Pending,
    Scheduled,
    Processing,
    Completed,
    Failed,
    Cancelled,
    DeadLettered
}
```

You may later introduce additional states if needed.

---

# 6. Phase 3 — SQL Server Database

Use Entity Framework Core with SQL Server.

Create:

```text
JobQueueDbContext
```

Tables:

```text
Jobs
JobAttempts
```

Optionally later:

```text
JobOutboxMessages
JobExecutionLogs
```

---

# 7. Jobs Table

Recommended initial schema:

```text
Jobs
------------------------------------------------
Id                  uniqueidentifier
Type                nvarchar(200)
Payload             nvarchar(max)
Status              int
Priority            int
Attempts            int
MaxAttempts         int

CreatedAt           datetime2
ScheduledAt         datetime2 nullable
StartedAt           datetime2 nullable
CompletedAt         datetime2 nullable
FailedAt            datetime2 nullable
CancelledAt         datetime2 nullable

LastError           nvarchar(max) nullable

IdempotencyKey      nvarchar(200) nullable
CorrelationId       uniqueidentifier nullable
WorkerId            nvarchar(200) nullable

RowVersion          rowversion
```

Create an index on:

```text
Status
ScheduledAt
```

And a unique index on:

```text
IdempotencyKey
```

when the key is provided.

---

# 8. Phase 4 — Job Creation API

Implement:

```http
POST /jobs
```

Request:

```json
{
  "type": "SendEmail",
  "payload": {
    "to": "user@example.com",
    "subject": "Welcome"
  }
}
```

Response:

```http
202 Accepted
```

Example:

```json
{
  "id": "guid",
  "status": "Pending"
}
```

The important architectural rule:

> The API should create/register the job and enqueue it. It should not execute the actual work.

---

# 9. Job Creation Flow

Implement the following flow:

```text
POST /jobs
     │
     ▼
Validate request
     │
     ▼
Check Idempotency-Key
     │
     ▼
Create Job in SQL Server
     │
     ▼
Publish Job message
     │
     ▼
Return 202
```

Do not perform:

```text
Send email
Generate PDF
Call external API
Process expensive operation
```

inside the HTTP request.

---

# 10. Phase 5 — Job Status API

Implement:

```http
GET /jobs/{id}
```

Example:

```json
{
  "id": "guid",
  "type": "SendEmail",
  "status": "Processing",
  "attempts": 1,
  "createdAt": "...",
  "startedAt": "..."
}
```

Also implement:

```http
GET /jobs
```

with filtering:

```text
GET /jobs?status=Failed
GET /jobs?type=SendEmail
GET /jobs?page=1&pageSize=20
```

---

# 11. Phase 6 — RabbitMQ

Run RabbitMQ locally using Docker.

Infrastructure:

```text
SQL Server
RabbitMQ
```

Docker Compose should eventually provide:

```text
sqlserver
rabbitmq
```

RabbitMQ will be responsible for transporting work between the API and workers.

---

# 12. Phase 7 — MassTransit

Configure MassTransit with RabbitMQ.

Create a message contract:

```csharp
public record ProcessJob
{
    public Guid JobId { get; init; }
    public string Type { get; init; } = default!;
}
```

The message should contain the job identifier rather than unnecessarily duplicating the entire database record.

Flow:

```text
API
 │
 │ ProcessJob
 ▼
RabbitMQ
 │
 ▼
MassTransit Consumer
 │
 ▼
Worker
 │
 ▼
SQL Server
```

---

# 13. Phase 8 — Job Consumer

Create:

```text
ProcessJobConsumer
```

Conceptually:

```text
Consume ProcessJob
        │
        ▼
Load Job from SQL Server
        │
        ▼
Check current status
        │
        ├── Completed → ignore
        ├── Cancelled → ignore
        ├── Processing → handle carefully
        └── Pending → continue
        │
        ▼
Mark Processing
        │
        ▼
Execute job
        │
        ▼
Mark Completed
```

This is where idempotency becomes important.

---

# 14. Phase 9 — Job Handlers

Do not put every job implementation inside the consumer.

Use a handler abstraction:

```text
IJobHandler
```

For example:

```text
IJobHandler
    │
    ├── SendEmailJobHandler
    ├── GenerateInvoiceJobHandler
    ├── UpdateInventoryJobHandler
    └── NotifyUserJobHandler
```

Resolve the handler based on:

```text
Job.Type
```

Example:

```text
SendEmail
    ↓
SendEmailJobHandler
```

This allows the queue infrastructure to remain independent from business operations.

---

# 15. Phase 10 — Worker Concurrency

Configure MassTransit concurrency.

Start with:

```text
Concurrency = 1
```

Then test:

```text
Concurrency = 5
Concurrency = 10
Concurrency = 20
```

Example architecture:

```text
RabbitMQ
   │
   ├── Worker 1 ── 5 concurrent jobs
   ├── Worker 2 ── 5 concurrent jobs
   └── Worker 3 ── 5 concurrent jobs
```

The important concept:

> Multiple worker processes can consume from the same queue.

RabbitMQ distributes messages among consumers.

---

# 16. Phase 11 — Retry

Implement retries for transient failures.

Example:

```text
Attempt 1
   ↓ failure
Retry
   ↓
Attempt 2
   ↓ failure
Retry
   ↓
Attempt 3
   ↓ failure
Retry
   ↓
Dead Letter
```

Start with:

```text
MaxAttempts = 3
```

But distinguish between:

### Transient errors

Examples:

```text
HTTP 503
Database timeout
Temporary network failure
RabbitMQ unavailable
```

These may be retried.

### Permanent errors

Examples:

```text
Invalid email address
Invalid job payload
Missing required business entity
```

These generally should not be retried indefinitely.

---

# 17. Phase 12 — Exponential Backoff

Implement:

```text
Delay = BaseDelay × 2^(Attempt - 1)
```

Example with a 5-second base:

```text
Attempt 1 → 5 seconds
Attempt 2 → 10 seconds
Attempt 3 → 20 seconds
Attempt 4 → 40 seconds
```

Add jitter to avoid many workers retrying simultaneously.

For example:

```text
5s + random jitter
10s + random jitter
20s + random jitter
```

Use MassTransit's retry/redelivery facilities where appropriate rather than implementing message retry loops manually.

---

# 18. Phase 13 — Dead-Letter Queue

After maximum retry attempts:

```text
RabbitMQ
    │
    ▼
Job Consumer
    │
    ▼
Failure
    │
    ▼
Retry
    │
    ▼
Retry
    │
    ▼
Maximum attempts reached
    │
    ▼
Dead Letter Queue
```

At the application level, update:

```text
Job.Status = DeadLettered
```

Store:

```text
Attempts
LastError
FailedAt
```

The DLQ should allow operators to inspect failed jobs.

---

# 19. Phase 14 — Failed Jobs API

Implement:

```http
GET /jobs/failed
```

and/or:

```http
GET /jobs?status=Failed
```

Return:

```json
{
  "items": [
    {
      "id": "...",
      "type": "SendEmail",
      "attempts": 3,
      "lastError": "...",
      "failedAt": "..."
    }
  ]
}
```

---

# 20. Phase 15 — Manual Retry

Allow operators to retry a failed job.

```http
POST /jobs/{id}/retry
```

Flow:

```text
Failed Job
    │
    ▼
POST /retry
    │
    ▼
Reset retry state
    │
    ▼
Pending
    │
    ▼
RabbitMQ
    │
    ▼
Worker
```

Do not create a duplicate job unless that is explicitly the desired behavior.

---

# 21. Phase 16 — Job Cancellation

Implement:

```http
POST /jobs/{id}/cancel
```

Cancellation behavior depends on job state.

### Pending

Easy:

```text
Pending
   ↓
Cancelled
```

### Scheduled

```text
Scheduled
   ↓
Cancelled
```

### Processing

This is more difficult.

You should use:

```csharp
CancellationToken
```

where possible.

Architecture:

```text
Job
 ↓
CancellationToken
 ↓
Job Handler
 ↓
External operation
```

A running job should periodically check:

```csharp
cancellationToken.ThrowIfCancellationRequested();
```

Important:

> Cancellation is cooperative. You cannot safely kill arbitrary running code from outside.

---

# 22. Phase 17 — Scheduled Jobs

Allow:

```http
POST /jobs
```

with:

```json
{
  "type": "SendEmail",
  "payload": {},
  "scheduledAt": "2026-09-11T15:00:00Z"
}
```

The job starts as:

```text
Scheduled
```

A scheduler/dispatcher finds jobs where:

```text
ScheduledAt <= UTC now
AND
Status = Scheduled
```

Then publishes:

```text
ProcessJob
```

and changes the state to:

```text
Pending
```

or directly to a dispatchable state.

---

# 23. Scheduled Job Dispatcher

Create an ASP.NET Core:

```text
BackgroundService
```

Example responsibility:

```text
Every few seconds
       │
       ▼
Find scheduled jobs
       │
       ▼
Acquire jobs safely
       │
       ▼
Publish ProcessJob
       │
       ▼
Mark jobs as dispatched
```

Important:

> Multiple application instances may run the scheduler.

Therefore, you must prevent two instances from dispatching the same job.

Use SQL Server concurrency controls/transactions rather than relying on:

```csharp
if (job.Status == Scheduled)
```

alone.

---

# 24. Phase 18 — Job Idempotency

This is one of the most important parts of the project.

Assume:

```text
Worker receives job
       ↓
Sends email
       ↓
Worker crashes
       ↓
Message is delivered again
       ↓
Email gets sent twice
```

The queue system must assume:

> Messages may be delivered more than once.

Implement an idempotency key:

```http
Idempotency-Key: abc-123
```

Store it in SQL Server.

Create a unique constraint/index.

Then:

```text
Same idempotency key
        ↓
Existing job found
        ↓
Return existing job
```

This prevents duplicate job creation.

---

# 25. Execution Idempotency

Job creation idempotency is not enough.

The actual handler should also consider duplicate execution.

For example:

```text
SendEmail
```

should have a strategy that prevents duplicate side effects where possible.

For database operations, use unique business keys or an inbox/processed-message table.

For external APIs, use an idempotency key if the external API supports one.

---

# 26. Phase 19 — Job Attempts

Create:

```text
JobAttempts
```

Example:

```text
JobAttempts
----------------------------------
Id
JobId
AttemptNumber
StartedAt
CompletedAt
FailedAt
Error
WorkerId
```

This gives you an execution history.

Example:

```text
Job #123

Attempt 1
  10:00
  Failed
  HTTP 503

Attempt 2
  10:00:10
  Failed
  HTTP 503

Attempt 3
  10:00:30
  Completed
```

This is much better than storing only the last error.

---

# 27. Phase 20 — Job State Machine

Define valid transitions.

```text
                    ┌──────────────┐
                    │   Scheduled  │
                    └──────┬───────┘
                           │
                           ▼
                    ┌──────────────┐
              ┌────►│    Pending   │
              │     └──────┬───────┘
              │            │
              │            ▼
              │     ┌──────────────┐
              │     │  Processing  │
              │     └──────┬───────┘
              │            │
          retry            │ success
              │            ▼
              │     ┌──────────────┐
              │     │  Completed   │
              │     └──────────────┘
              │
              ▼
       ┌──────────────┐
       │    Failed    │
       └──────┬───────┘
              │
       max attempts
              │
              ▼
       ┌──────────────┐
       │ DeadLettered │
       └──────────────┘

Pending/Scheduled
       │
       ▼
   Cancelled
```

Centralize state transitions instead of allowing arbitrary code to update `Status`.

---

# 28. Phase 21 — Concurrency Control

You will have multiple workers attempting to update the same job.

Example:

```text
Worker 1 ─────┐
              ├── Job #123
Worker 2 ─────┘
```

Use SQL Server optimistic concurrency.

The `rowversion` column is useful here.

Example:

```text
Worker 1 reads version 5
Worker 2 reads version 5

Worker 1 updates → version 6

Worker 2 attempts update using version 5
                    ↓
                 conflict
```

Worker 2 should detect the concurrency conflict rather than silently overwriting Worker 1.

---

# 29. Phase 22 — Outbox Pattern

Once the basic implementation works, introduce the **Transactional Outbox**.

The problem:

```text
Save job to SQL Server
        ↓
SQL transaction commits
        ↓
RabbitMQ publish fails
```

Now you have:

```text
Job exists
but
Message doesn't
```

The opposite problem is also possible.

Use an outbox:

```text
SQL Transaction
     │
     ├── Insert Job
     │
     └── Insert Outbox Message
             │
             ▼
        Transaction commits
             │
             ▼
      Outbox Dispatcher
             │
             ▼
          RabbitMQ
```

MassTransit has support for transactional/outbox patterns that should be evaluated here.

---

# 30. Phase 23 — Observability

Add structured logging.

Every job should have:

```text
JobId
CorrelationId
JobType
AttemptNumber
WorkerId
```

Example:

```text
Processing job

JobId: ...
JobType: SendEmail
Attempt: 2
Worker: worker-03
```

Use correlation IDs across:

```text
HTTP request
    ↓
Job
    ↓
RabbitMQ message
    ↓
Consumer
    ↓
Handler
```

---

# 31. Phase 24 — Health Checks

Add:

```http
/health
```

and:

```http
/health/ready
```

Check:

```text
SQL Server
RabbitMQ
```

Worker readiness should fail if critical infrastructure is unavailable.

---

# 32. Phase 25 — Graceful Shutdown

Workers must handle application shutdown correctly.

When receiving:

```text
SIGTERM
```

the worker should:

```text
Stop accepting new work
        ↓
Allow current work to finish
        ↓
Respect cancellation
        ↓
Disconnect from RabbitMQ
        ↓
Shutdown
```

Test this explicitly.

---

# 33. Phase 26 — Docker Compose

Create a development environment:

```text
docker-compose.yml
```

Services:

```text
sqlserver
rabbitmq
api
worker
```

Eventually:

```text
                    ┌───────────┐
                    │   API     │
                    └─────┬─────┘
                          │
                 ┌────────┴────────┐
                 ▼                 ▼
           SQL Server          RabbitMQ
                                   │
                     ┌─────────────┼─────────────┐
                     ▼             ▼             ▼
                  Worker 1      Worker 2      Worker 3
```

---

# 34. Phase 27 — API Endpoints

Final API should contain approximately:

## Create

```http
POST /jobs
```

## Get

```http
GET /jobs/{id}
```

## List

```http
GET /jobs
```

## Cancel

```http
POST /jobs/{id}/cancel
```

## Retry

```http
POST /jobs/{id}/retry
```

## Failed

```http
GET /jobs/failed
```

## Attempts

```http
GET /jobs/{id}/attempts
```

Optional:

```http
DELETE /jobs/{id}
```

Avoid physically deleting jobs initially. Job history is useful for debugging and auditing.

---

# 35. Example API

## Create immediate job

```http
POST /jobs
Idempotency-Key: order-123-email
Content-Type: application/json
```

```json
{
  "type": "SendEmail",
  "payload": {
    "to": "customer@example.com",
    "template": "OrderCreated",
    "orderId": "123"
  }
}
```

Response:

```http
202 Accepted
```

```json
{
  "id": "8f1...",
  "status": "Pending"
}
```

---

# 36. Example Scheduled Job

```http
POST /jobs
```

```json
{
  "type": "GenerateInvoice",
  "payload": {
    "orderId": "123"
  },
  "scheduledAt": "2026-09-11T18:00:00Z"
}
```

Response:

```json
{
  "id": "8f1...",
  "status": "Scheduled"
}
```

---

# 37. Example Order Architecture

After finishing the generic job system, implement a realistic example.

```text
POST /orders
      │
      ▼
Create Order
      │
      ▼
Save SQL transaction
      │
      ▼
Publish OrderCreated
      │
      ▼
Return 202/200
      │
      ▼
RabbitMQ
      │
      ├───────────────┐
      │               │
      ▼               ▼
Send Email       Update Inventory
      │               │
      ▼               ▼
Generate Invoice  Notify User
```

This demonstrates why asynchronous processing is useful.

---

# 38. Suggested Job Types

Implement these progressively:

### 1. SendEmail

Simple simulated job.

```text
SendEmail
```

### 2. GenerateInvoice

Simulate CPU/file work.

```text
GenerateInvoice
```

### 3. UpdateInventory

Database-oriented operation.

```text
UpdateInventory
```

### 4. NotifyUser

External HTTP/API operation.

```text
NotifyUser
```

This gives you different failure scenarios to test.

---

# 39. Testing Strategy

## Unit Tests

Test:

```text
Job state transitions
Retry calculation
Exponential backoff
Idempotency logic
Job validation
Cancellation
Handler resolution
```

Example:

```text
Given a failed job
When retry is requested
Then status becomes Pending
```

---

# 40. Integration Tests

Test the real infrastructure where possible.

```text
API
 ↓
SQL Server
 ↓
RabbitMQ
 ↓
Consumer
 ↓
Handler
```

Important scenarios:

### Successful job

```text
Create
→ Consume
→ Complete
```

### Failed job

```text
Create
→ Consume
→ Fail
```

### Retry

```text
Fail
→ Retry
→ Retry
→ Success
```

### Dead letter

```text
Fail
→ Retry
→ Retry
→ Max attempts
→ Dead Letter
```

### Cancellation

```text
Create
→ Cancel
→ Worker ignores/cancels
```

### Idempotency

```text
Request 1
Request 2 with same key
       ↓
One Job
```

---

# 41. Failure Scenarios You Must Test

Do not consider the project complete until these scenarios work.

## Worker crashes

```text
Worker starts job
     ↓
Worker crashes
     ↓
Message becomes available again
     ↓
Another worker processes it
```

## RabbitMQ temporarily unavailable

```text
API
 ↓
RabbitMQ unavailable
```

Determine whether the API should:

* fail the request, or
* rely on an outbox and return successfully.

The second approach is preferable once the outbox is implemented.

## SQL Server temporarily unavailable

Test:

```text
Worker
 ↓
SQL unavailable
```

The job should not be incorrectly marked completed.

## Duplicate message

```text
Message
Message
   ↓
Same JobId
```

The handler should safely handle duplicate delivery.

## Worker shutdown

```text
Processing
   ↓
SIGTERM
   ↓
Graceful cancellation
```

---

# 42. Security

Add basic API security after the core queue works.

Consider:

```text
Authentication
Authorization
Rate limiting
Request size limits
Payload validation
```

Do not allow arbitrary clients to submit unlimited jobs.

For production-like behavior, protect administrative operations such as:

```text
Retry
Cancel
Dead-letter inspection
```

---

# 43. Metrics

Track at least:

```text
Jobs created
Jobs completed
Jobs failed
Jobs cancelled
Jobs dead-lettered

Processing duration
Queue delay
Retry count
Active workers
```

Useful derived metrics:

```text
Average processing time
P95 processing time
Failure rate
Retry rate
Queue depth
```

---

# 44. Recommended Implementation Order

Do not implement all features simultaneously.

Build in this order:

```text
Phase 1
Solution structure
        ↓
Phase 2
Job entity + SQL Server
        ↓
Phase 3
POST /jobs
        ↓
Phase 4
RabbitMQ
        ↓
Phase 5
MassTransit consumer
        ↓
Phase 6
Job handler abstraction
        ↓
Phase 7
Job status
        ↓
Phase 8
Concurrency
        ↓
Phase 9
Retry
        ↓
Phase 10
Exponential backoff
        ↓
Phase 11
Dead-letter handling
        ↓
Phase 12
Cancellation
        ↓
Phase 13
Scheduled jobs
        ↓
Phase 14
Idempotency
        ↓
Phase 15
Job attempts/history
        ↓
Phase 16
Optimistic concurrency
        ↓
Phase 17
Outbox
        ↓
Phase 18
Observability
        ↓
Phase 19
Docker
        ↓
Phase 20
Integration tests
```

---

# 45. Milestone-Based Development

## Milestone 1 — Hello Queue

Requirements:

* ASP.NET Core API
* SQL Server
* RabbitMQ
* MassTransit
* Worker

Feature:

```text
POST /jobs
   ↓
RabbitMQ
   ↓
Worker
   ↓
Console log
```

Nothing else.

---

## Milestone 2 — Persistent Jobs

Add:

* Job entity
* EF Core
* Job status
* `GET /jobs/{id}`

Flow:

```text
POST /jobs
   ↓
SQL Server
   ↓
RabbitMQ
   ↓
Worker
   ↓
SQL Server → Completed
```

---

## Milestone 3 — Reliability

Add:

* Retry
* Exponential backoff
* Attempt history
* Dead-letter handling

---

## Milestone 4 — Production Concepts

Add:

* Idempotency
* Optimistic concurrency
* Cancellation
* Scheduled jobs
* Worker concurrency

---

## Milestone 5 — Distributed Systems

Add:

* Transactional outbox
* Multiple worker instances
* Graceful shutdown
* Correlation IDs
* Health checks
* Metrics

---

## Milestone 6 — Real Application

Build:

```text
Order API
    ↓
OrderCreated
    ↓
RabbitMQ
    ├── Send confirmation email
    ├── Update inventory
    ├── Generate invoice
    └── Notify user
```

This becomes the final demonstration of the architecture.

---

# 46. Definition of Done

The project is complete when you can demonstrate:

### Job Creation

```text
POST /jobs
```

creates a persistent job and returns immediately.

### Asynchronous Processing

The HTTP request does not execute the expensive operation.

### Multiple Workers

You can run:

```text
Worker 1
Worker 2
Worker 3
```

and jobs are distributed between them.

### Concurrency

Each worker can process multiple jobs concurrently.

### Retry

Transient failures are retried automatically.

### Backoff

Retries use increasing delays.

### Dead Letter

Jobs exceeding maximum attempts become dead-lettered.

### Status

You can inspect:

```text
Pending
Scheduled
Processing
Completed
Failed
Cancelled
DeadLettered
```

### Cancellation

Pending/scheduled jobs can be cancelled, and running jobs cooperate with cancellation.

### Scheduling

Jobs can execute at a future time.

### Idempotency

Duplicate API requests do not create duplicate jobs.

### Duplicate Delivery

Duplicate messages do not produce unintended duplicate side effects.

### Persistence

All important job state survives worker restarts.

### Recovery

A worker crash does not permanently lose a job.

### Outbox

Database state and message publication are eventually consistent without the classic dual-write problem.

### Observability

You can determine:

```text
What happened?
When?
Which worker?
Which attempt?
Why did it fail?
How long did it take?
```

---

# 47. Final Architecture

The final system should look approximately like this:

```text
                           ┌─────────────────┐
                           │     Client      │
                           └────────┬────────┘
                                    │
                                    ▼
                           ┌─────────────────┐
                           │ ASP.NET Core 10 │
                           │      API        │
                           └────────┬────────┘
                                    │
                              SQL Transaction
                                    │
                         ┌──────────┴──────────┐
                         │                     │
                         ▼                     ▼
                    ┌─────────┐          ┌──────────┐
                    │  Jobs   │          │  Outbox  │
                    │   DB    │          │ Messages │
                    └─────────┘          └────┬─────┘
                                              │
                                              ▼
                                       ┌─────────────┐
                                       │  RabbitMQ   │
                                       └──────┬──────┘
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
                                    ┌────────────────┐
                                    │ Job Handlers   │
                                    ├────────────────┤
                                    │ SendEmail      │
                                    │ Invoice        │
                                    │ Inventory      │
                                    │ Notification   │
                                    └───────┬────────┘
                                            │
                                            ▼
                                     External Systems
```

---

# 48. The Most Important Concepts to Focus On

Don't treat this project as simply:

> "How do I configure RabbitMQ?"

The real learning objectives are the distributed-system problems underneath it:

```text
HTTP request
     ↓
Asynchronous message
     ↓
At-least-once delivery
     ↓
Duplicate execution
     ↓
Idempotency
     ↓
Failure
     ↓
Retry
     ↓
Backoff
     ↓
Dead letter
     ↓
Recovery
```

And:

```text
SQL transaction
      +
Message publication
      ↓
Dual-write problem
      ↓
Transactional Outbox
```

And:

```text
Multiple workers
      ↓
Concurrent processing
      ↓
Race conditions
      ↓
Optimistic concurrency
      ↓
Safe state transitions
```

Those three areas—**delivery semantics, failure handling, and consistency between the database and message broker**—are where this project becomes much more valuable than a basic CRUD application.

# 49. Suggested Final Project Demonstration

At the end, run:

```text
API
Worker 1
Worker 2
Worker 3
RabbitMQ
SQL Server
```

Then submit:

```http
POST /jobs
```

with 100 jobs.

Demonstrate:

```text
100 jobs created
        ↓
RabbitMQ
        ↓
3 workers
        ↓
Concurrent processing
        ↓
Some jobs intentionally fail
        ↓
Automatic retries
        ↓
Exponential backoff
        ↓
Successful jobs → Completed
Permanent failures → DeadLettered
```

Then kill:

```text
Worker 2
```

while jobs are processing.

Demonstrate that the system recovers and the remaining workers continue processing.

Finally, demonstrate:

```text
Duplicate HTTP request
        ↓
Idempotency
        ↓
One job
```

and:

```text
SQL transaction
        ↓
Outbox
        ↓
RabbitMQ
        ↓
Worker
```

That gives you a practical understanding of how real-world asynchronous backend systems are designed.

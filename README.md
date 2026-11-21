# Wallet Ledger API

A .NET 10 Web API that keeps wallet balances in a double-entry ledger and supports deposits, withdrawals, wallet-to-wallet transfers and reversals.

The focus is correctness of money movement: concurrent requests cannot overdraw a wallet, a retried request never moves money twice, and recorded history is never modified.

## What it does

- Every money movement is a journal transaction made of at least two entries that sum to zero. Money is never created or lost.
- Deposits and withdrawals are double-entry too: one settlement (system) account per currency represents money entering and leaving the platform.
- Entries are immutable. A wrong transaction is never deleted or updated; it is corrected by a new reversal transaction that posts the opposite entries.
- The account statement shows the running balance (`balanceAfter`) on every line.

## Technical choices and why

**Layers.** Three projects: `Api` (Minimal API endpoints, HTTP mapping), `Core` (domain rules, no package dependencies) and `Infrastructure` (EF Core, services). Services use the DbContext directly. There is no repository, MediatR or AutoMapper layer; at this size they would only add indirection.

**Money.** Amounts are stored as `long` minor units (`15050` = 150.50 TRY), so there is no floating point rounding. Supported currencies are TRY, EUR and USD. Transfers between different currencies are out of scope.

**Concurrency: pessimistic row locks.** Every money movement runs in a single database transaction: lock the account rows with `SELECT ... FOR UPDATE`, check the rules, write, commit.

- Locks are always taken in account id order, so A→B and B→A transfers arriving at the same time cannot deadlock.
- This was chosen over optimistic concurrency with retries because heavy traffic on the same wallet is an expected case. Requests queue up instead of causing retry storms.
- PostgreSQL's `xmin` column is also used as a concurrency token on accounts. If the lock were ever skipped by a bug, the conflicting write is rejected with 409; the tests verify this case separately.
- The database itself is the last line of defence: `CHECK (allow_negative OR balance >= 0)` keeps wallet balances from going below zero, and a partial unique index allows a transaction to be reversed at most once.

**Idempotency.** Every endpoint that moves money requires an `Idempotency-Key` header.

- The key is stored in the same Postgres transaction as the ledger entries. A separate store such as Redis was not used, because it would allow inconsistent states like "money moved but the key was not saved".
- A second concurrent request with the same key waits on the primary key until the first request finishes. Once the first one commits, the second gets the stored outcome. No distributed lock or "in progress" state is needed.
- The same key with the same body returns the original response with an `Idempotent-Replayed: true` header. The same key with a different body returns `422 idempotency-key-reused`.
- Business rule failures (for example `insufficient-funds`) are stored too. A retry with the same key returns the same result even if the balance has changed in the meantime. A new attempt needs a new key.

**Error responses.** All errors use RFC 7807 (`application/problem+json`) with `type`, `title`, `status`, `detail`, `instance`, `traceId` and a machine-readable `code`.

- Expected business rule violations travel as a `Result`, not as exceptions, and map to 400, 404, 409 or 422.
- Unexpected exceptions are never swallowed. The global handler logs them first, then returns 500 without leaking details. Deadlocks and transient connection errors return 503.

**Statement paging.** Keyset pagination is used instead of `OFFSET` (`WHERE account_id = @id AND id < @cursor ORDER BY id DESC`), served by the `(account_id, id DESC)` index. Rows do not shift between pages when new entries arrive, and deep pages do not get slower.

**Tests.** Unit tests exercise the rules in `Core` without a database. Integration tests start a real PostgreSQL 17 with Testcontainers; the EF InMemory provider was not used because it cannot reproduce row locks, check constraints or unique violations. Critical cases covered:

- Out of 50 concurrent 10.00 TRY transfers from a 100.00 TRY balance, exactly 10 succeed.
- 100 concurrent transfers in opposite directions do not deadlock.
- 20 concurrent requests with the same key produce a single posting.
- Out of 10 concurrent reversals of the same transaction, only one succeeds.
- A reconciliation check verifies that every transaction's entries sum to 0 and every account balance equals the sum of its entries.

**Known limits.**

- Every deposit and withdrawal locks the single settlement row of its currency, so deposits and withdrawals in the same currency are processed one at a time. Wallet-to-wallet transfers are not affected. At higher volume the settlement account could be split into several sub-accounts.
- There is no authentication; it was left out of scope on purpose.
- There is no cleanup job for the `idempotency_records` table. The `created_at` index is there for a future TTL cleanup.
- The EF Core `Database.Command` and `Update` log categories are set to `Critical`, so expected unique violations that the code handles do not produce error logs. Unexpected failures are still logged with full exception details by the global handler.

## Running locally

Requirements: Docker. For local development, also the .NET 10 SDK.

To start everything with one command:

```bash
docker compose up --build
```

The API listens on `http://localhost:8080` and applies migrations on startup. Sample requests are in [`src/Wallet.Api/wallet.http`](src/Wallet.Api/wallet.http); run them top to bottom with VS Code REST Client, Visual Studio or Rider. The OpenAPI document is at `/openapi/v1.json`.

To run the API from an IDE or with `dotnet run`, start only the database and apply the migrations first:

```bash
docker compose up -d db
dotnet tool restore
dotnet ef database update -p src/Wallet.Infrastructure -s src/Wallet.Api
dotnet run --project src/Wallet.Api
```

In this mode the API listens on `http://localhost:5095`.

Tests only need a running Docker engine; the integration tests start and stop their own PostgreSQL container:

```bash
dotnet test
```

CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) runs on every push and pull request: Release build, a check that the EF model matches the migrations, and the unit and integration tests.

## Endpoints

| Method | Route | Description |
|---|---|---|
| POST | `/accounts` | Open a wallet (`ownerName`, `currency`) |
| GET | `/accounts/{id}` | Balance and status |
| GET | `/accounts/{id}/entries?cursor=&limit=` | Statement, newest first, `limit` up to 100 |
| POST | `/accounts/{id}/freeze` | Freeze: the wallet can neither send nor receive money |
| POST | `/accounts/{id}/unfreeze` | Lift the freeze |
| POST | `/accounts/{id}/close` | Close (only when the balance is 0) |
| POST | `/accounts/{id}/deposits` | Deposit, `Idempotency-Key` required |
| POST | `/accounts/{id}/withdrawals` | Withdraw, `Idempotency-Key` required |
| POST | `/transfers` | Wallet-to-wallet transfer, `Idempotency-Key` required |
| GET | `/transfers/{id}` | A transaction and its entries (deposits and withdrawals included) |
| POST | `/transfers/{id}/reversal` | Reverse a transaction, `Idempotency-Key` required |
| GET | `/health` | Application and database health |

Example request:

```bash
curl -X POST http://localhost:8080/transfers \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: rent-2026-11-elif-kerem" \
  -d '{"fromAccountId":"<elif-id>","toAccountId":"<kerem-id>","amountMinor":175000,"currency":"TRY","description":"Rent share - November"}'
```

When the balance is not enough, the response looks like this:

```json
{
  "type": "https://wallet-api.local/problems/insufficient-funds",
  "title": "Insufficient funds",
  "status": 422,
  "detail": "Account 01a1fb96-0d49-7102-bf94-4f0510bc5843 does not have enough balance for this operation.",
  "instance": "/transfers",
  "code": "insufficient-funds",
  "traceId": "00-0204678c02ff0d29521f795b6ce3b6f8-b28b3c274da694ea-00"
}
```

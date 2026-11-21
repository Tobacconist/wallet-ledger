using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wallet.Core.Accounts;
using Wallet.Core.Errors;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Idempotency;
using Wallet.Infrastructure.Persistence;
using Wallet.Infrastructure.Persistence.Configurations;

namespace Wallet.Infrastructure.Services;

public sealed record LedgerOutcome(Result<LedgerTransaction> Result, bool Replayed);

public sealed class LedgerService(WalletDbContext db, AccountLocker locker, TimeProvider time)
{
    public async Task<LedgerOutcome> DepositAsync(
        IdempotencyKey key, Guid accountId, long amountMinor, string currencyCode, string? description, CancellationToken ct)
    {
        var money = ParseMoney(amountMinor, currencyCode);
        if (!money.IsSuccess)
        {
            return new(money.Error, Replayed: false);
        }

        var settlementId = SystemAccounts.SettlementId(money.Value.Currency);
        return await PostAsync(
            key,
            [accountId, settlementId],
            (accounts, now) => Task.FromResult(LedgerPostings.Deposit(accounts[settlementId], accounts[accountId], money.Value, description, now)),
            ct);
    }

    public async Task<LedgerOutcome> WithdrawAsync(
        IdempotencyKey key, Guid accountId, long amountMinor, string currencyCode, string? description, CancellationToken ct)
    {
        var money = ParseMoney(amountMinor, currencyCode);
        if (!money.IsSuccess)
        {
            return new(money.Error, Replayed: false);
        }

        var settlementId = SystemAccounts.SettlementId(money.Value.Currency);
        return await PostAsync(
            key,
            [accountId, settlementId],
            (accounts, now) => Task.FromResult(LedgerPostings.Withdraw(accounts[accountId], accounts[settlementId], money.Value, description, now)),
            ct);
    }

    public async Task<LedgerOutcome> TransferAsync(
        IdempotencyKey key, Guid fromAccountId, Guid toAccountId, long amountMinor, string currencyCode, string? description, CancellationToken ct)
    {
        var money = ParseMoney(amountMinor, currencyCode);
        if (!money.IsSuccess)
        {
            return new(money.Error, Replayed: false);
        }

        if (fromAccountId == toAccountId)
        {
            return new(DomainErrors.SameAccountTransfer(), Replayed: false);
        }

        return await PostAsync(
            key,
            [fromAccountId, toAccountId],
            (accounts, now) => Task.FromResult(LedgerPostings.Transfer(accounts[fromAccountId], accounts[toAccountId], money.Value, description, now)),
            ct);
    }

    public async Task<LedgerOutcome> ReverseAsync(IdempotencyKey key, Guid transactionId, CancellationToken ct)
    {
        var original = await GetTransactionAsync(transactionId, ct);
        if (!original.IsSuccess)
        {
            return new(original.Error, Replayed: false);
        }

        var accountIds = original.Value.Entries.Select(e => e.AccountId).Distinct().ToArray();
        return await PostAsync(
            key,
            accountIds,
            async (accounts, now) =>
            {
                // Checked under the row locks, so a concurrent reversal is either visible here or still waiting.
                if (await db.Transactions.AnyAsync(t => t.ReversesTransactionId == transactionId, ct))
                {
                    return DomainErrors.AlreadyReversed(transactionId);
                }

                return LedgerPostings.Reverse(original.Value, accounts, now);
            },
            ct);
    }

    public async Task<Result<LedgerTransaction>> GetTransactionAsync(Guid id, CancellationToken ct)
    {
        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Entries.OrderBy(e => e.Id))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return transaction is null ? DomainErrors.TransactionNotFound(id) : transaction;
    }

    private static Result<Money> ParseMoney(long amountMinor, string currencyCode)
    {
        var currency = Currency.Parse(currencyCode);
        return currency.IsSuccess ? Money.Create(amountMinor, currency.Value) : currency.Error;
    }

    // Lock order: idempotency key first, then accounts by id.
    private async Task<LedgerOutcome> PostAsync(
        IdempotencyKey key,
        Guid[] accountIds,
        Func<IReadOnlyDictionary<Guid, Account>, DateTimeOffset, Task<Result<LedgerTransaction>>> post,
        CancellationToken ct)
    {
        if (await RecordExistsAsync(key, ct))
        {
            return await ReplayAsync(key, ct);
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
        var now = time.GetDbUtcNow();
        var record = IdempotencyRecord.Start(key, now);
        db.IdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotencyRecordConfiguration.PrimaryKeyName,
        })
        {
            await dbTransaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ReplayAsync(key, ct);
        }

        var accounts = await locker.LockAsync(accountIds, ct);
        var result = FindMissing(accountIds, accounts) is { } missingId
            ? DomainErrors.AccountNotFound(missingId)
            : await post(accounts, now);

        if (result.IsSuccess)
        {
            db.Transactions.Add(result.Value);
            record.Succeed(result.Value.Id);
        }
        else
        {
            record.Fail(result.Error);
        }

        await db.SaveChangesAsync(ct);
        await dbTransaction.CommitAsync(ct);
        return new(result, Replayed: false);
    }

    private Task<bool> RecordExistsAsync(IdempotencyKey key, CancellationToken ct) =>
        db.IdempotencyRecords.AnyAsync(r => r.Key == key.Key && r.Operation == key.Operation, ct);

    private async Task<LedgerOutcome> ReplayAsync(IdempotencyKey key, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords
            .AsNoTracking()
            .SingleAsync(r => r.Key == key.Key && r.Operation == key.Operation, ct);

        if (record.RequestHash != key.RequestHash)
        {
            return new(DomainErrors.IdempotencyKeyReused(key.Key), Replayed: false);
        }

        if (record.TransactionId is { } transactionId)
        {
            return new(await GetTransactionAsync(transactionId, ct), Replayed: true);
        }

        var error = record.StoredError()
            ?? throw new InvalidOperationException($"Idempotency record '{key.Key}' has neither a transaction nor an error.");
        return new(error, Replayed: true);
    }

    private static Guid? FindMissing(Guid[] ids, Dictionary<Guid, Account> found)
    {
        foreach (var id in ids)
        {
            if (!found.ContainsKey(id))
            {
                return id;
            }
        }

        return null;
    }
}

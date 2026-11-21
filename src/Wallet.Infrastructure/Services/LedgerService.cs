using Microsoft.EntityFrameworkCore;
using Wallet.Core.Accounts;
using Wallet.Core.Errors;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Persistence;

namespace Wallet.Infrastructure.Services;

public sealed class LedgerService(WalletDbContext db, AccountLocker locker, TimeProvider time)
{
    public async Task<Result<LedgerTransaction>> DepositAsync(
        Guid accountId, long amountMinor, string currencyCode, string? description, CancellationToken ct)
    {
        var money = ParseMoney(amountMinor, currencyCode);
        if (!money.IsSuccess)
        {
            return money.Error;
        }

        var settlementId = SystemAccounts.SettlementId(money.Value.Currency);
        return await PostAsync(
            [accountId, settlementId],
            (accounts, now) => LedgerPostings.Deposit(accounts[settlementId], accounts[accountId], money.Value, description, now),
            ct);
    }

    public async Task<Result<LedgerTransaction>> WithdrawAsync(
        Guid accountId, long amountMinor, string currencyCode, string? description, CancellationToken ct)
    {
        var money = ParseMoney(amountMinor, currencyCode);
        if (!money.IsSuccess)
        {
            return money.Error;
        }

        var settlementId = SystemAccounts.SettlementId(money.Value.Currency);
        return await PostAsync(
            [accountId, settlementId],
            (accounts, now) => LedgerPostings.Withdraw(accounts[accountId], accounts[settlementId], money.Value, description, now),
            ct);
    }

    public async Task<Result<LedgerTransaction>> TransferAsync(
        Guid fromAccountId, Guid toAccountId, long amountMinor, string currencyCode, string? description, CancellationToken ct)
    {
        var money = ParseMoney(amountMinor, currencyCode);
        if (!money.IsSuccess)
        {
            return money.Error;
        }

        if (fromAccountId == toAccountId)
        {
            return DomainErrors.SameAccountTransfer();
        }

        return await PostAsync(
            [fromAccountId, toAccountId],
            (accounts, now) => LedgerPostings.Transfer(accounts[fromAccountId], accounts[toAccountId], money.Value, description, now),
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

    // Lock, validate, write and commit happen in one database transaction. Returning early on a business
    // error disposes the transaction without commit, which rolls back and releases the row locks.
    private async Task<Result<LedgerTransaction>> PostAsync(
        Guid[] accountIds,
        Func<IReadOnlyDictionary<Guid, Account>, DateTimeOffset, Result<LedgerTransaction>> post,
        CancellationToken ct)
    {
        await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);

        var accounts = await locker.LockAsync(accountIds, ct);
        foreach (var id in accountIds)
        {
            if (!accounts.ContainsKey(id))
            {
                return DomainErrors.AccountNotFound(id);
            }
        }

        var result = post(accounts, time.GetUtcNow());
        if (!result.IsSuccess)
        {
            return result.Error;
        }

        db.Transactions.Add(result.Value);
        await db.SaveChangesAsync(ct);
        await dbTransaction.CommitAsync(ct);
        return result.Value;
    }
}

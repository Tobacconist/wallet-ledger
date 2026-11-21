using Wallet.Core.Monetary;

namespace Wallet.Core.Ledger;

// Positive amount increases the account balance, negative decreases it. Entries are never updated or deleted.
public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    internal LedgerEntry(Guid transactionId, Guid accountId, long amount, Currency currency, long balanceAfter, DateTimeOffset createdAt)
    {
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
        BalanceAfter = balanceAfter;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public Guid TransactionId { get; private set; }

    public Guid AccountId { get; private set; }

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public long BalanceAfter { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

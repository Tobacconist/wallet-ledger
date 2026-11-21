using Wallet.Core.Monetary;

namespace Wallet.Core.Ledger;

// Positive amounts increase the balance, negative amounts decrease it.
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

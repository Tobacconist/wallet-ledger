using Wallet.Core.Accounts;
using Wallet.Core.Errors;
using Wallet.Core.Monetary;

namespace Wallet.Core.Ledger;

public static class LedgerPostings
{
    public static Result<LedgerTransaction> Deposit(Account settlement, Account wallet, Money amount, string? description, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(wallet);
        EnsureSettlement(settlement);

        if (wallet.Type != AccountType.Wallet)
        {
            return DomainErrors.SystemAccountOperation(wallet.Id);
        }

        return Posting.Post(
            TransactionType.Deposit,
            amount.Currency,
            [new(settlement, -amount.MinorUnits), new(wallet, amount.MinorUnits)],
            description,
            null,
            now);
    }

    public static Result<LedgerTransaction> Withdraw(Account wallet, Account settlement, Money amount, string? description, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(settlement);
        EnsureSettlement(settlement);

        if (wallet.Type != AccountType.Wallet)
        {
            return DomainErrors.SystemAccountOperation(wallet.Id);
        }

        return Posting.Post(
            TransactionType.Withdrawal,
            amount.Currency,
            [new(wallet, -amount.MinorUnits), new(settlement, amount.MinorUnits)],
            description,
            null,
            now);
    }

    public static Result<LedgerTransaction> Transfer(Account from, Account to, Money amount, string? description, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from.Id == to.Id)
        {
            return DomainErrors.SameAccountTransfer();
        }

        if (from.Type != AccountType.Wallet)
        {
            return DomainErrors.SystemAccountOperation(from.Id);
        }

        if (to.Type != AccountType.Wallet)
        {
            return DomainErrors.SystemAccountOperation(to.Id);
        }

        return Posting.Post(
            TransactionType.Transfer,
            amount.Currency,
            [new(from, -amount.MinorUnits), new(to, amount.MinorUnits)],
            description,
            null,
            now);
    }

    // A reversal never touches the original entries; it posts the exact opposite legs as a new transaction.
    public static Result<LedgerTransaction> Reverse(LedgerTransaction original, IReadOnlyDictionary<Guid, Account> accounts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(accounts);

        if (original.Type == TransactionType.Reversal)
        {
            return DomainErrors.CannotReverseReversal(original.Id);
        }

        var legs = original.Entries
            .Select(e => new PostingLeg(accounts[e.AccountId], -e.Amount))
            .ToList();

        return Posting.Post(
            TransactionType.Reversal,
            original.Entries[0].Currency,
            legs,
            $"Reversal of {original.Id}",
            original.Id,
            now);
    }

    private static void EnsureSettlement(Account account)
    {
        if (account.Type != AccountType.System)
        {
            throw new InvalidOperationException($"Account {account.Id} is not a settlement account.");
        }
    }
}

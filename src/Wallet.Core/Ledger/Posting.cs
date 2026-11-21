using Wallet.Core.Accounts;
using Wallet.Core.Errors;
using Wallet.Core.Monetary;

namespace Wallet.Core.Ledger;

public readonly record struct PostingLeg(Account Account, long Amount);

public static class Posting
{
    // Structural problems (unbalanced legs, duplicate accounts) are programming errors and throw;
    // business rule violations are returned so the caller can map them to a 4xx response.
    public static Result<LedgerTransaction> Post(
        TransactionType type,
        Currency currency,
        IReadOnlyList<PostingLeg> legs,
        string? description,
        Guid? reversesTransactionId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(legs);

        if (legs.Count < 2)
        {
            throw new InvalidOperationException("A posting needs at least two legs.");
        }

        if (legs.Any(l => l.Amount == 0))
        {
            throw new InvalidOperationException("A posting leg cannot have a zero amount.");
        }

        if (legs.Select(l => l.Account.Id).Distinct().Count() != legs.Count)
        {
            throw new InvalidOperationException("Each account can appear only once in a posting.");
        }

        if (legs.Sum(l => l.Amount) != 0)
        {
            throw new InvalidOperationException("Posting legs must sum to zero.");
        }

        foreach (var leg in legs)
        {
            if (leg.Account.Currency != currency)
            {
                return DomainErrors.CurrencyMismatch(leg.Account.Id, leg.Account.Currency.Code, currency.Code);
            }
        }

        foreach (var leg in legs)
        {
            var active = leg.Account.EnsureActive();
            if (!active.IsSuccess)
            {
                return active.Error;
            }
        }

        foreach (var leg in legs.Where(l => l.Amount < 0))
        {
            var funds = leg.Account.EnsureCanDebit(-leg.Amount);
            if (!funds.IsSuccess)
            {
                return funds.Error;
            }
        }

        var transaction = new LedgerTransaction(Guid.CreateVersion7(now), type, description, reversesTransactionId, now);
        foreach (var leg in legs)
        {
            var balanceAfter = leg.Account.Apply(leg.Amount);
            transaction.AddEntry(new LedgerEntry(transaction.Id, leg.Account.Id, leg.Amount, currency, balanceAfter, now));
        }

        return transaction;
    }
}

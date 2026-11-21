using System.ComponentModel.DataAnnotations;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;

namespace Wallet.Api.Contracts;

public sealed record MoneyMovementRequest(
    [property: Range(1, Money.MaxMinorUnits)] long AmountMinor,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    [property: StringLength(LedgerTransaction.DescriptionMaxLength)] string? Description);

public sealed record TransferRequest(
    [property: Required] Guid? FromAccountId,
    [property: Required] Guid? ToAccountId,
    [property: Range(1, Money.MaxMinorUnits)] long AmountMinor,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    [property: StringLength(LedgerTransaction.DescriptionMaxLength)] string? Description);

public sealed record EntryResponse(long Id, Guid AccountId, long Amount, string Currency, long BalanceAfter);

public sealed record TransactionResponse(
    Guid Id,
    string Type,
    string? Description,
    Guid? ReversesTransactionId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EntryResponse> Entries)
{
    public static TransactionResponse From(LedgerTransaction transaction) => new(
        transaction.Id,
        transaction.Type.ToString(),
        transaction.Description,
        transaction.ReversesTransactionId,
        transaction.CreatedAt,
        [.. transaction.Entries.Select(e => new EntryResponse(e.Id, e.AccountId, e.Amount, e.Currency.Code, e.BalanceAfter))]);
}

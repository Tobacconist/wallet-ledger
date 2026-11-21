using System.ComponentModel.DataAnnotations;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Services;

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

public sealed record StatementLineResponse(
    long EntryId,
    Guid TransactionId,
    string Type,
    string? Description,
    long Amount,
    string Currency,
    long BalanceAfter,
    DateTimeOffset CreatedAt);

public sealed record StatementResponse(IReadOnlyList<StatementLineResponse> Items, long? NextCursor)
{
    public static StatementResponse From(StatementPage page) => new(
        [.. page.Lines.Select(l => new StatementLineResponse(
            l.EntryId, l.TransactionId, l.Type.ToString(), l.Description, l.Amount, l.Currency.Code, l.BalanceAfter, l.CreatedAt))],
        page.NextCursor);
}

using System.ComponentModel.DataAnnotations;
using Wallet.Core.Accounts;

namespace Wallet.Api.Contracts;

public sealed record CreateAccountRequest(
    [property: Required, StringLength(Account.OwnerNameMaxLength, MinimumLength = 1)] string OwnerName,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency);

public sealed record AccountResponse(
    Guid Id,
    string Type,
    string OwnerName,
    string Currency,
    long Balance,
    string Status,
    DateTimeOffset CreatedAt)
{
    public static AccountResponse From(Account account) => new(
        account.Id,
        account.Type.ToString(),
        account.OwnerName,
        account.Currency.Code,
        account.Balance,
        account.Status.ToString(),
        account.CreatedAt);
}

using Wallet.Core.Errors;
using Wallet.Core.Monetary;

namespace Wallet.Core.Accounts;

public sealed class Account
{
    public const int OwnerNameMaxLength = 200;

    private Account()
    {
        OwnerName = string.Empty;
    }

    public Guid Id { get; private set; }

    public AccountType Type { get; private set; }

    public string? SystemCode { get; private set; }

    public string OwnerName { get; private set; }

    public Currency Currency { get; private set; }

    public long Balance { get; private set; }

    public bool AllowNegative { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Account> OpenWallet(string ownerName, Currency currency, DateTimeOffset now)
    {
        var name = ownerName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > OwnerNameMaxLength)
        {
            return DomainErrors.InvalidOwnerName();
        }

        return new Account
        {
            Id = Guid.CreateVersion7(now),
            Type = AccountType.Wallet,
            OwnerName = name,
            Currency = currency,
            Status = AccountStatus.Active,
            CreatedAt = now,
        };
    }

    // Settlement accounts mirror money entering or leaving the platform, so they are allowed to go negative.
    public static Account CreateSystem(Guid id, string systemCode, Currency currency, DateTimeOffset now) => new()
    {
        Id = id,
        Type = AccountType.System,
        SystemCode = systemCode,
        OwnerName = systemCode,
        Currency = currency,
        AllowNegative = true,
        Status = AccountStatus.Active,
        CreatedAt = now,
    };

    public Result Freeze()
    {
        if (Type == AccountType.System)
        {
            return DomainErrors.SystemAccountOperation(Id);
        }

        if (Status == AccountStatus.Closed)
        {
            return DomainErrors.AccountClosed(Id);
        }

        Status = AccountStatus.Frozen;
        return Result.Success;
    }

    public Result Unfreeze()
    {
        if (Type == AccountType.System)
        {
            return DomainErrors.SystemAccountOperation(Id);
        }

        if (Status == AccountStatus.Closed)
        {
            return DomainErrors.AccountClosed(Id);
        }

        Status = AccountStatus.Active;
        return Result.Success;
    }

    public Result Close()
    {
        if (Type == AccountType.System)
        {
            return DomainErrors.SystemAccountOperation(Id);
        }

        if (Balance != 0)
        {
            return DomainErrors.AccountHasBalance(Id);
        }

        Status = AccountStatus.Closed;
        return Result.Success;
    }

    internal Result EnsureActive() =>
        Status == AccountStatus.Active ? Result.Success : DomainErrors.AccountNotActive(Id, Status.ToString());

    internal Result EnsureCanDebit(long minorUnits) =>
        AllowNegative || Balance >= minorUnits ? Result.Success : DomainErrors.InsufficientFunds(Id);

    internal long Apply(long signedAmount)
    {
        Balance = checked(Balance + signedAmount);
        return Balance;
    }
}

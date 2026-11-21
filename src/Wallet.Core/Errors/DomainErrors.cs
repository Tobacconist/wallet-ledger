namespace Wallet.Core.Errors;

public static class DomainErrors
{
    public static DomainError InvalidAmount(long minorUnits) =>
        new("invalid-amount", $"Amount must be between 1 and {Monetary.Money.MaxMinorUnits} minor units, got {minorUnits}.", ErrorKind.Validation);

    public static DomainError UnsupportedCurrency(string? code) =>
        new("unsupported-currency", $"Currency '{code}' is not supported.", ErrorKind.Validation);

    public static DomainError InvalidOwnerName() =>
        new("invalid-owner-name", "Owner name must be between 1 and 200 characters.", ErrorKind.Validation);

    public static DomainError SameAccountTransfer() =>
        new("same-account-transfer", "Source and destination accounts must be different.", ErrorKind.BusinessRule);

    public static DomainError CurrencyMismatch(Guid accountId, string expected, string actual) =>
        new("currency-mismatch", $"Account {accountId} holds {expected}, but the operation uses {actual}.", ErrorKind.BusinessRule);

    public static DomainError InsufficientFunds(Guid accountId) =>
        new("insufficient-funds", $"Account {accountId} does not have enough balance for this operation.", ErrorKind.BusinessRule);

    public static DomainError AccountNotActive(Guid accountId, string status) =>
        new("account-not-active", $"Account {accountId} is {status} and cannot be used for money movement.", ErrorKind.BusinessRule);

    public static DomainError AccountHasBalance(Guid accountId) =>
        new("account-has-balance", $"Account {accountId} must have zero balance before it can be closed.", ErrorKind.BusinessRule);

    public static DomainError AccountClosed(Guid accountId) =>
        new("account-closed", $"Account {accountId} is closed.", ErrorKind.BusinessRule);

    public static DomainError SystemAccountOperation(Guid accountId) =>
        new("system-account-operation", $"Account {accountId} is a system account and cannot be used for this operation.", ErrorKind.BusinessRule);

    public static DomainError CannotReverseReversal(Guid transactionId) =>
        new("cannot-reverse-reversal", $"Transaction {transactionId} is itself a reversal and cannot be reversed.", ErrorKind.BusinessRule);
}

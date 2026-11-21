namespace Wallet.Core.Errors;

public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
}

public sealed record DomainError(string Code, string Message, ErrorKind Kind);

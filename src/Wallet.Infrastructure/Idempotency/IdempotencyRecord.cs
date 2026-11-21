using Wallet.Core.Errors;

namespace Wallet.Infrastructure.Idempotency;

// Stores the outcome of the first request rather than the raw HTTP body. Replays rebuild the response through
// the same mapping, so the body is identical while this layer stays unaware of HTTP.
public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
        Key = string.Empty;
        Operation = string.Empty;
        RequestHash = string.Empty;
    }

    public string Key { get; private set; }

    public string Operation { get; private set; }

    public string RequestHash { get; private set; }

    public Guid? TransactionId { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public ErrorKind? ErrorKind { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Start(IdempotencyKey key, DateTimeOffset now) => new()
    {
        Key = key.Key,
        Operation = key.Operation,
        RequestHash = key.RequestHash,
        CreatedAt = now,
    };

    public void Succeed(Guid transactionId) => TransactionId = transactionId;

    public void Fail(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        ErrorCode = error.Code;
        ErrorMessage = error.Message;
        ErrorKind = error.Kind;
    }

    public DomainError? StoredError() =>
        ErrorCode is null ? null : new DomainError(ErrorCode, ErrorMessage ?? string.Empty, ErrorKind ?? Core.Errors.ErrorKind.BusinessRule);
}

using System.Security.Cryptography;
using System.Text.Json;
using Wallet.Core.Errors;
using Wallet.Infrastructure.Idempotency;

namespace Wallet.Api.Idempotency;

internal static class IdempotencyRequest
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    // The typed request record is serialized with fixed property order, so equal payloads always hash equally.
    public static Result<IdempotencyKey> Create<TRequest>(string? headerValue, string operation, TRequest request)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return DomainErrors.IdempotencyKeyRequired();
        }

        if (headerValue.Length > IdempotencyKey.MaxKeyLength)
        {
            return DomainErrors.InvalidIdempotencyKey(IdempotencyKey.MaxKeyLength);
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        return new IdempotencyKey(headerValue, operation, hash);
    }
}

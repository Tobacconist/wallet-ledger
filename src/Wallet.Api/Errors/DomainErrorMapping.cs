using Microsoft.AspNetCore.Http.HttpResults;
using Wallet.Core.Errors;

namespace Wallet.Api.Errors;

internal static class DomainErrorMapping
{
    public static ProblemHttpResult ToProblem(this DomainError error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => throw new InvalidOperationException($"Unmapped error kind {error.Kind}."),
        };

        return TypedResults.Problem(
            statusCode: status,
            title: Humanize(error.Code),
            detail: error.Message,
            type: ProblemTypes.For(error.Code),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    private static string Humanize(string code)
    {
        var text = code.Replace('-', ' ');
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}

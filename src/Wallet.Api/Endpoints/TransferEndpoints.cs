using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallet.Api.Contracts;
using Wallet.Api.Errors;
using Wallet.Api.Idempotency;
using Wallet.Core.Errors;
using Wallet.Core.Ledger;
using Wallet.Infrastructure.Services;

namespace Wallet.Api.Endpoints;

internal static class TransferEndpoints
{
    public const string GetTransactionRoute = "GetTransaction";

    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transfers").WithTags("Transfers");

        group.MapPost("/", TransferAsync);
        group.MapGet("/{id:guid}", GetAsync).WithName(GetTransactionRoute);

        return app;
    }

    public static Results<CreatedAtRoute<TransactionResponse>, ProblemHttpResult> ToResponse(LedgerOutcome outcome, HttpContext http)
    {
        if (outcome.Replayed)
        {
            http.Response.Headers[IdempotencyRequest.ReplayedHeaderName] = "true";
        }

        return ToCreated(outcome.Result);
    }

    private static Results<CreatedAtRoute<TransactionResponse>, ProblemHttpResult> ToCreated(Result<LedgerTransaction> result) =>
        result.IsSuccess
            ? TypedResults.CreatedAtRoute(TransactionResponse.From(result.Value), GetTransactionRoute, new { id = result.Value.Id })
            : result.Error.ToProblem();

    private static async Task<Results<CreatedAtRoute<TransactionResponse>, ProblemHttpResult>> TransferAsync(
        TransferRequest request,
        [FromHeader(Name = IdempotencyRequest.HeaderName)] string? idempotencyKey,
        LedgerService ledger,
        HttpContext http,
        CancellationToken ct)
    {
        var key = IdempotencyRequest.Create(idempotencyKey, "transfer", request);
        if (!key.IsSuccess)
        {
            return key.Error.ToProblem();
        }

        var outcome = await ledger.TransferAsync(
            key.Value, request.FromAccountId!.Value, request.ToAccountId!.Value, request.AmountMinor, request.Currency, request.Description, ct);
        return ToResponse(outcome, http);
    }

    private static async Task<Results<Ok<TransactionResponse>, ProblemHttpResult>> GetAsync(
        Guid id, LedgerService ledger, CancellationToken ct)
    {
        var result = await ledger.GetTransactionAsync(id, ct);
        return result.IsSuccess ? TypedResults.Ok(TransactionResponse.From(result.Value)) : result.Error.ToProblem();
    }
}

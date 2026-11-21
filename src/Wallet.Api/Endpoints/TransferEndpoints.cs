using Microsoft.AspNetCore.Http.HttpResults;
using Wallet.Api.Contracts;
using Wallet.Api.Errors;
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

        group.MapPost("/", async (TransferRequest request, LedgerService ledger, CancellationToken ct) =>
            ToCreated(await ledger.TransferAsync(
                request.FromAccountId!.Value, request.ToAccountId!.Value, request.AmountMinor, request.Currency, request.Description, ct)));

        group.MapGet("/{id:guid}", GetAsync).WithName(GetTransactionRoute);

        return app;
    }

    public static Results<CreatedAtRoute<TransactionResponse>, ProblemHttpResult> ToCreated(Result<LedgerTransaction> result) =>
        result.IsSuccess
            ? TypedResults.CreatedAtRoute(TransactionResponse.From(result.Value), GetTransactionRoute, new { id = result.Value.Id })
            : result.Error.ToProblem();

    private static async Task<Results<Ok<TransactionResponse>, ProblemHttpResult>> GetAsync(
        Guid id, LedgerService ledger, CancellationToken ct)
    {
        var result = await ledger.GetTransactionAsync(id, ct);
        return result.IsSuccess ? TypedResults.Ok(TransactionResponse.From(result.Value)) : result.Error.ToProblem();
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using Wallet.Api.Contracts;
using Wallet.Api.Errors;
using Wallet.Core.Errors;
using Wallet.Infrastructure.Services;

namespace Wallet.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/accounts").WithTags("Accounts");

        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync).WithName("GetAccount");
        group.MapPost("/{id:guid}/freeze", (Guid id, AccountService accounts, CancellationToken ct) =>
            StatusChangeAsync(accounts.FreezeAsync(id, ct)));
        group.MapPost("/{id:guid}/unfreeze", (Guid id, AccountService accounts, CancellationToken ct) =>
            StatusChangeAsync(accounts.UnfreezeAsync(id, ct)));
        group.MapPost("/{id:guid}/close", (Guid id, AccountService accounts, CancellationToken ct) =>
            StatusChangeAsync(accounts.CloseAsync(id, ct)));

        group.MapPost("/{id:guid}/deposits", async (Guid id, MoneyMovementRequest request, LedgerService ledger, CancellationToken ct) =>
            TransferEndpoints.ToCreated(await ledger.DepositAsync(id, request.AmountMinor, request.Currency, request.Description, ct)));
        group.MapPost("/{id:guid}/withdrawals", async (Guid id, MoneyMovementRequest request, LedgerService ledger, CancellationToken ct) =>
            TransferEndpoints.ToCreated(await ledger.WithdrawAsync(id, request.AmountMinor, request.Currency, request.Description, ct)));

        return app;
    }

    private static async Task<Results<CreatedAtRoute<AccountResponse>, ProblemHttpResult>> CreateAsync(
        CreateAccountRequest request, AccountService accounts, CancellationToken ct)
    {
        var result = await accounts.OpenWalletAsync(request.OwnerName, request.Currency, ct);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        return TypedResults.CreatedAtRoute(AccountResponse.From(result.Value), "GetAccount", new { id = result.Value.Id });
    }

    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> GetAsync(
        Guid id, AccountService accounts, CancellationToken ct)
    {
        var result = await accounts.GetAsync(id, ct);
        return result.IsSuccess ? TypedResults.Ok(AccountResponse.From(result.Value)) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> StatusChangeAsync(Task<Result> operation)
    {
        var result = await operation;
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }
}

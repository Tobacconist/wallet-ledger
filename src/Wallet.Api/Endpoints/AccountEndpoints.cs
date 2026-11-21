using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallet.Api.Contracts;
using Wallet.Api.Errors;
using Wallet.Api.Idempotency;
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

        group.MapPost("/{id:guid}/deposits", DepositAsync);
        group.MapPost("/{id:guid}/withdrawals", WithdrawAsync);
        group.MapGet("/{id:guid}/entries", GetStatementAsync);

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

    private static async Task<Results<CreatedAtRoute<TransactionResponse>, ProblemHttpResult>> DepositAsync(
        Guid id,
        MoneyMovementRequest request,
        [FromHeader(Name = IdempotencyRequest.HeaderName)] string? idempotencyKey,
        LedgerService ledger,
        HttpContext http,
        CancellationToken ct)
    {
        var key = IdempotencyRequest.Create(idempotencyKey, $"deposit:{id}", request);
        if (!key.IsSuccess)
        {
            return key.Error.ToProblem();
        }

        var outcome = await ledger.DepositAsync(key.Value, id, request.AmountMinor, request.Currency, request.Description, ct);
        return TransferEndpoints.ToResponse(outcome, http);
    }

    private static async Task<Results<CreatedAtRoute<TransactionResponse>, ProblemHttpResult>> WithdrawAsync(
        Guid id,
        MoneyMovementRequest request,
        [FromHeader(Name = IdempotencyRequest.HeaderName)] string? idempotencyKey,
        LedgerService ledger,
        HttpContext http,
        CancellationToken ct)
    {
        var key = IdempotencyRequest.Create(idempotencyKey, $"withdrawal:{id}", request);
        if (!key.IsSuccess)
        {
            return key.Error.ToProblem();
        }

        var outcome = await ledger.WithdrawAsync(key.Value, id, request.AmountMinor, request.Currency, request.Description, ct);
        return TransferEndpoints.ToResponse(outcome, http);
    }

    private static async Task<Results<Ok<StatementResponse>, ProblemHttpResult>> GetStatementAsync(
        Guid id, long? cursor, int? limit, StatementService statements, CancellationToken ct)
    {
        var page = await statements.GetPageAsync(id, cursor, limit, ct);
        return page.IsSuccess ? TypedResults.Ok(StatementResponse.From(page.Value)) : page.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> StatusChangeAsync(Task<Result> operation)
    {
        var result = await operation;
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }
}

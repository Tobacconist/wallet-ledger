using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Wallet.Api.Errors;

// Only for failures that are not part of normal business flow. Everything is logged before a ProblemDetails
// response is written, so nothing is swallowed silently.
internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "bad-request", "Bad request"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency-conflict", "Concurrency conflict"),
            // Database constraints are the backstop behind the application checks (e.g. one reversal per transaction).
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "conflict", "Conflict"),
            PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure }
                => (StatusCodes.Status503ServiceUnavailable, "transient-database-error", "Transient database error"),
            NpgsqlException { IsTransient: true }
                => (StatusCodes.Status503ServiceUnavailable, "transient-database-error", "Transient database error"),
            _ => (StatusCodes.Status500InternalServerError, "internal-error", "Unexpected error"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            LogHandled(logger, exception, status, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = ProblemTypes.For(code),
                Extensions = { ["code"] = code },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request failed with {Status} for {Method} {Path}")]
    private static partial void LogHandled(ILogger logger, Exception exception, int status, string method, string path);
}

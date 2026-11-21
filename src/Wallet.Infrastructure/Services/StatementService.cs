using Microsoft.EntityFrameworkCore;
using Wallet.Core.Errors;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Persistence;

namespace Wallet.Infrastructure.Services;

public sealed record StatementLine(
    long EntryId,
    Guid TransactionId,
    TransactionType Type,
    string? Description,
    long Amount,
    Currency Currency,
    long BalanceAfter,
    DateTimeOffset CreatedAt);

public sealed record StatementPage(IReadOnlyList<StatementLine> Lines, long? NextCursor);

public sealed class StatementService(WalletDbContext db)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Keyset pagination on (account_id, id DESC): stable under concurrent inserts and served straight from the index,
    // unlike OFFSET which shifts rows between pages and gets slower the deeper the client pages.
    public async Task<Result<StatementPage>> GetPageAsync(Guid accountId, long? cursor, int? limit, CancellationToken ct)
    {
        var pageSize = limit ?? DefaultPageSize;
        if (pageSize is < 1 or > MaxPageSize)
        {
            return DomainErrors.InvalidPageSize(MaxPageSize);
        }

        if (cursor is <= 0)
        {
            return DomainErrors.InvalidCursor();
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, ct))
        {
            return DomainErrors.AccountNotFound(accountId);
        }

        var lines = await (
                from e in db.Entries
                join t in db.Transactions on e.TransactionId equals t.Id
                where e.AccountId == accountId && (cursor == null || e.Id < cursor)
                orderby e.Id descending
                select new StatementLine(e.Id, e.TransactionId, t.Type, t.Description, e.Amount, e.Currency, e.BalanceAfter, e.CreatedAt))
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = lines.Count > pageSize;
        if (hasMore)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return new StatementPage(lines, hasMore ? lines[^1].EntryId : null);
    }
}

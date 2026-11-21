using Microsoft.EntityFrameworkCore;
using Wallet.Core.Accounts;
using Wallet.Infrastructure.Persistence;

namespace Wallet.Infrastructure.Services;

public sealed class AccountLocker(WalletDbContext db)
{
    // Must run inside an open transaction. Rows are locked in id order, so two transfers moving money in
    // opposite directions between the same accounts queue up behind each other instead of deadlocking.
    // xmin is selected explicitly because SELECT * does not include system columns.
    public async Task<Dictionary<Guid, Account>> LockAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToArray();
        var accounts = await db.Accounts
            .FromSql($"SELECT *, xmin FROM accounts WHERE id = ANY({distinct}) ORDER BY id FOR UPDATE")
            .ToListAsync(ct);

        return accounts.ToDictionary(a => a.Id);
    }
}

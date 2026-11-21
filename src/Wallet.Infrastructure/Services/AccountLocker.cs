using Microsoft.EntityFrameworkCore;
using Wallet.Core.Accounts;
using Wallet.Infrastructure.Persistence;

namespace Wallet.Infrastructure.Services;

public sealed class AccountLocker(WalletDbContext db)
{
    // Call inside a transaction. Locking in id order keeps opposite transfers from deadlocking;
    // xmin is listed because SELECT * skips system columns.
    public async Task<Dictionary<Guid, Account>> LockAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToArray();
        var accounts = await db.Accounts
            .FromSql($"SELECT *, xmin FROM accounts WHERE id = ANY({distinct}) ORDER BY id FOR UPDATE")
            .ToListAsync(ct);

        return accounts.ToDictionary(a => a.Id);
    }
}

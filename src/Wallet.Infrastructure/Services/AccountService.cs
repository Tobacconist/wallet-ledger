using Microsoft.EntityFrameworkCore;
using Wallet.Core.Accounts;
using Wallet.Core.Errors;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Persistence;

namespace Wallet.Infrastructure.Services;

public sealed class AccountService(WalletDbContext db, TimeProvider time)
{
    public async Task<Result<Account>> OpenWalletAsync(string ownerName, string currencyCode, CancellationToken ct)
    {
        var currency = Currency.Parse(currencyCode);
        if (!currency.IsSuccess)
        {
            return currency.Error;
        }

        var account = Account.OpenWallet(ownerName, currency.Value, time.GetUtcNow());
        if (!account.IsSuccess)
        {
            return account.Error;
        }

        db.Accounts.Add(account.Value);
        await db.SaveChangesAsync(ct);
        return account.Value;
    }

    public async Task<Result<Account>> GetAsync(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        return account is null ? DomainErrors.AccountNotFound(id) : account;
    }

    public Task<Result> FreezeAsync(Guid id, CancellationToken ct) => ChangeStatusAsync(id, a => a.Freeze(), ct);

    public Task<Result> UnfreezeAsync(Guid id, CancellationToken ct) => ChangeStatusAsync(id, a => a.Unfreeze(), ct);

    // Close relies on the xmin concurrency token: if a deposit lands between the balance check and the save,
    // SaveChanges throws DbUpdateConcurrencyException instead of closing an account that now holds money.
    public Task<Result> CloseAsync(Guid id, CancellationToken ct) => ChangeStatusAsync(id, a => a.Close(), ct);

    private async Task<Result> ChangeStatusAsync(Guid id, Func<Account, Result> change, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
        {
            return DomainErrors.AccountNotFound(id);
        }

        var result = change(account);
        if (!result.IsSuccess)
        {
            return result;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}

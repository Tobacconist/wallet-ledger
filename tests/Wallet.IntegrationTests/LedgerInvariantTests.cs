using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallet.Infrastructure.Persistence;

namespace Wallet.IntegrationTests;

// Reconciliation over everything the other tests in this collection have written so far.
[Collection(ApiTestGroup.Name)]
public class LedgerInvariantTests(WalletApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Every_transaction_sums_to_zero_and_balances_match_entries()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 30000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        await _client.TransferAsync(elif, kerem, 12000);
        await _client.WithdrawAsync(kerem, 5000);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

        var unbalanced = await db.Entries
            .GroupBy(e => e.TransactionId)
            .Where(g => g.Sum(e => e.Amount) != 0)
            .CountAsync();
        Assert.Equal(0, unbalanced);

        var drifted = await db.Accounts
            .Where(a => a.Balance != (db.Entries.Where(e => e.AccountId == a.Id).Sum(e => (long?)e.Amount) ?? 0))
            .Select(a => a.Id)
            .ToListAsync();
        Assert.Empty(drifted);

        var lastEntries = await db.Entries
            .GroupBy(e => e.AccountId)
            .Select(g => g.OrderByDescending(e => e.Id).First())
            .ToListAsync();
        var balances = await db.Accounts.ToDictionaryAsync(a => a.Id, a => a.Balance);
        Assert.All(lastEntries, e => Assert.Equal(balances[e.AccountId], e.BalanceAfter));
    }
}

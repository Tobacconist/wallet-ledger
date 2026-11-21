using System.Net;

namespace Wallet.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class TransferConcurrencyTests(WalletApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Parallel_transfers_cannot_overdraw_the_sender()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 10000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");

        var responses = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => _client.TransferAsync(elif, kerem, 1000, description: "Market alışverişi")));

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(40, responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(0, await _client.BalanceAsync(elif));
        Assert.Equal(10000, await _client.BalanceAsync(kerem));
    }

    [Fact]
    public async Task Opposite_direction_transfers_do_not_deadlock()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 50000);
        var kerem = await _client.FundedWalletAsync("Kerem Aydın", 50000);

        var requests = Enumerable.Range(0, 100).Select(i => i % 2 == 0
            ? _client.TransferAsync(elif, kerem, 300, description: "Yemek hesabı")
            : _client.TransferAsync(kerem, elif, 300, description: "Yemek hesabı"));
        var responses = await Task.WhenAll(requests);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(50000, await _client.BalanceAsync(elif));
        Assert.Equal(50000, await _client.BalanceAsync(kerem));
    }

    [Fact]
    public async Task Parallel_deposits_and_withdrawals_keep_balance_consistent()
    {
        var zeynep = await _client.FundedWalletAsync("Zeynep Kaya", 5000);

        var requests = Enumerable.Range(0, 60).Select(i => i % 3 == 0
            ? _client.WithdrawAsync(zeynep, 700)
            : _client.DepositAsync(zeynep, 400));
        var responses = await Task.WhenAll(requests);

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        var deposited = responses.Where((r, i) => i % 3 != 0 && r.StatusCode == HttpStatusCode.Created).Count() * 400L;
        var withdrawn = responses.Where((r, i) => i % 3 == 0 && r.StatusCode == HttpStatusCode.Created).Count() * 700L;
        Assert.Equal(5000 + deposited - withdrawn, await _client.BalanceAsync(zeynep));
    }
}

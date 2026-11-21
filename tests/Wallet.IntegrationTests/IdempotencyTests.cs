using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallet.Infrastructure.Persistence;

namespace Wallet.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class IdempotencyTests(WalletApiFactory factory)
{
    private const string ReplayedHeader = "Idempotent-Replayed";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Money_movement_without_key_is_rejected()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.PostAsJsonAsync($"/accounts/{elif}/deposits", new { amountMinor = 1000, currency = "TRY" });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "idempotency-key-required");
        Assert.Equal(0, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Too_long_key_is_rejected()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.DepositAsync(elif, 1000, key: new string('k', 101));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid-idempotency-key");
    }

    [Fact]
    public async Task Retried_transfer_is_replayed_and_posted_once()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 50000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        var key = $"kira-kasim-{Guid.NewGuid()}";

        var first = await _client.TransferAsync(elif, kerem, 12000, description: "Kira payı - Kasım", key: key);
        var second = await _client.TransferAsync(elif, kerem, 12000, description: "Kira payı - Kasım", key: key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains(ReplayedHeader));
        Assert.Equal("true", second.Headers.GetValues(ReplayedHeader).Single());
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(38000, await _client.BalanceAsync(elif));
        Assert.Equal(12000, await _client.BalanceAsync(kerem));
    }

    [Fact]
    public async Task Concurrent_requests_with_same_key_create_one_transaction()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 50000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => _client.TransferAsync(elif, kerem, 5000, description: "Fatura payı", key: key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(19, responses.Count(r => r.Headers.Contains(ReplayedHeader)));

        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            ids.Add(body.GetProperty("id").GetGuid());
        }

        Assert.Single(ids);
        Assert.Equal(45000, await _client.BalanceAsync(elif));
        Assert.Equal(5000, await _client.BalanceAsync(kerem));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        Assert.Equal(1, await db.Entries.CountAsync(e => e.AccountId == kerem));
    }

    [Fact]
    public async Task Same_key_with_different_body_is_rejected()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 50000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        var key = Guid.NewGuid().ToString();
        (await _client.TransferAsync(elif, kerem, 1000, key: key)).EnsureSuccessStatusCode();

        var response = await _client.TransferAsync(elif, kerem, 2000, key: key);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "idempotency-key-reused");
        Assert.False(response.Headers.Contains(ReplayedHeader));
        Assert.Equal(49000, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Business_failure_is_stored_and_replayed_even_after_funds_arrive()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 1000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        var key = Guid.NewGuid().ToString();

        var first = await _client.TransferAsync(elif, kerem, 5000, key: key);
        await first.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "insufficient-funds");

        (await _client.DepositAsync(elif, 10000)).EnsureSuccessStatusCode();
        var retry = await _client.TransferAsync(elif, kerem, 5000, key: key);

        await retry.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "insufficient-funds");
        Assert.Equal("true", retry.Headers.GetValues(ReplayedHeader).Single());

        var withNewKey = await _client.TransferAsync(elif, kerem, 5000);
        Assert.Equal(HttpStatusCode.Created, withNewKey.StatusCode);
        Assert.Equal(6000, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Same_key_on_different_operations_is_independent()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");
        var key = Guid.NewGuid().ToString();

        var deposit = await _client.DepositAsync(elif, 3000, key: key);
        var withdrawal = await _client.WithdrawAsync(elif, 1000, key: key);

        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withdrawal.StatusCode);
        Assert.False(withdrawal.Headers.Contains(ReplayedHeader));
        Assert.Equal(2000, await _client.BalanceAsync(elif));
    }
}

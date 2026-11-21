using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Persistence;

namespace Wallet.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class ReversalTests(WalletApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reversal_restores_balances_and_keeps_original_entries()
    {
        var (elif, kerem, transferId) = await TransferAsync(40000, 12500);

        var response = await ReverseAsync(transferId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reversal = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reversal", reversal.GetProperty("type").GetString());
        Assert.Equal(transferId, reversal.GetProperty("reversesTransactionId").GetGuid());
        Assert.Equal(40000, await _client.BalanceAsync(elif));
        Assert.Equal(0, await _client.BalanceAsync(kerem));

        var original = await _client.GetFromJsonAsync<JsonElement>($"/transfers/{transferId}");
        Assert.Equal("Transfer", original.GetProperty("type").GetString());
        Assert.Equal(2, original.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task Transaction_can_be_reversed_only_once()
    {
        var (_, _, transferId) = await TransferAsync(40000, 12500);
        (await ReverseAsync(transferId)).EnsureSuccessStatusCode();

        var second = await ReverseAsync(transferId);

        await second.AssertProblemAsync(HttpStatusCode.Conflict, "already-reversed");
    }

    [Fact]
    public async Task Concurrent_reversals_with_different_keys_post_only_one()
    {
        var (elif, kerem, transferId) = await TransferAsync(40000, 12500);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ReverseAsync(transferId)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var conflict in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await conflict.AssertProblemAsync(HttpStatusCode.Conflict, "already-reversed");
        }

        Assert.Equal(40000, await _client.BalanceAsync(elif));
        Assert.Equal(0, await _client.BalanceAsync(kerem));
    }

    [Fact]
    public async Task Retried_reversal_with_same_key_is_replayed()
    {
        var (_, _, transferId) = await TransferAsync(40000, 12500);
        var key = Guid.NewGuid().ToString();

        var first = await ReverseAsync(transferId, key);
        var retry = await ReverseAsync(transferId, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(first.Headers.Location, retry.Headers.Location);
    }

    [Fact]
    public async Task Reversal_cannot_be_reversed()
    {
        var (_, _, transferId) = await TransferAsync(40000, 12500);
        var reversal = await (await ReverseAsync(transferId)).Content.ReadFromJsonAsync<JsonElement>();

        var response = await ReverseAsync(reversal.GetProperty("id").GetGuid());

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "cannot-reverse-reversal");
    }

    [Fact]
    public async Task Reversal_fails_when_recipient_already_spent_the_money()
    {
        var (elif, kerem, transferId) = await TransferAsync(40000, 12500);
        (await _client.WithdrawAsync(kerem, 10000)).EnsureSuccessStatusCode();

        var response = await ReverseAsync(transferId);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "insufficient-funds");
        Assert.Equal(27500, await _client.BalanceAsync(elif));
        Assert.Equal(2500, await _client.BalanceAsync(kerem));
    }

    [Fact]
    public async Task Deposit_can_be_reversed_back_to_settlement()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");
        var settlement = SystemAccounts.SettlementId(Currency.TRY);
        var settlementBefore = await _client.BalanceAsync(settlement);
        var deposit = await (await _client.DepositAsync(elif, 8000)).Content.ReadFromJsonAsync<JsonElement>();

        var response = await ReverseAsync(deposit.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, await _client.BalanceAsync(elif));
        Assert.Equal(settlementBefore, await _client.BalanceAsync(settlement));
    }

    [Fact]
    public async Task Reversing_unknown_transaction_returns_not_found()
    {
        var response = await ReverseAsync(Guid.CreateVersion7());

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "transaction-not-found");
    }

    [Fact]
    public async Task Reversal_without_key_is_rejected()
    {
        var (_, _, transferId) = await TransferAsync(40000, 12500);

        var response = await _client.PostAsync($"/transfers/{transferId}/reversal", null);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "idempotency-key-required");
    }

    private async Task<(Guid Elif, Guid Kerem, Guid TransferId)> TransferAsync(long funded, long amount)
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", funded);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        var response = await _client.TransferAsync(elif, kerem, amount, description: "Yanlış alıcıya gönderim");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (elif, kerem, body.GetProperty("id").GetGuid());
    }

    private Task<HttpResponseMessage> ReverseAsync(Guid transactionId, string? key = null) =>
        _client.PostWithKeyAsync<object?>($"/transfers/{transactionId}/reversal", null, key);
}

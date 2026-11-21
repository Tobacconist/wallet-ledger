using System.Net.Http.Json;
using System.Text.Json;

namespace Wallet.IntegrationTests;

internal static class WalletApiClient
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static async Task<Guid> CreateWalletAsync(this HttpClient client, string ownerName, string currency = "TRY")
    {
        var response = await client.PostAsJsonAsync("/accounts", new { ownerName, currency });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> DepositAsync(
        this HttpClient client, Guid accountId, long amountMinor, string currency = "TRY", string? key = null) =>
        client.PostWithKeyAsync($"/accounts/{accountId}/deposits", new { amountMinor, currency, description = "Maaş yatırma" }, key);

    public static Task<HttpResponseMessage> WithdrawAsync(
        this HttpClient client, Guid accountId, long amountMinor, string currency = "TRY", string? key = null) =>
        client.PostWithKeyAsync($"/accounts/{accountId}/withdrawals", new { amountMinor, currency, description = "ATM çekimi" }, key);

    public static Task<HttpResponseMessage> TransferAsync(
        this HttpClient client,
        Guid fromAccountId,
        Guid toAccountId,
        long amountMinor,
        string currency = "TRY",
        string? description = null,
        string? key = null) =>
        client.PostWithKeyAsync("/transfers", new { fromAccountId, toAccountId, amountMinor, currency, description }, key);

    public static async Task<Guid> FundedWalletAsync(this HttpClient client, string ownerName, long amountMinor, string currency = "TRY")
    {
        var id = await client.CreateWalletAsync(ownerName, currency);
        (await client.DepositAsync(id, amountMinor, currency)).EnsureSuccessStatusCode();
        return id;
    }

    public static async Task<long> BalanceAsync(this HttpClient client, Guid accountId)
    {
        var account = await client.GetFromJsonAsync<JsonElement>($"/accounts/{accountId}");
        return account.GetProperty("balance").GetInt64();
    }

    // Every money movement needs a key; tests that do not care about replay get a fresh one per call.
    public static async Task<HttpResponseMessage> PostWithKeyAsync<T>(this HttpClient client, string url, T body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(IdempotencyHeader, key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}

using System.Net.Http.Json;
using System.Text.Json;

namespace Wallet.IntegrationTests;

internal static class WalletApiClient
{
    public static async Task<Guid> CreateWalletAsync(this HttpClient client, string ownerName, string currency = "TRY")
    {
        var response = await client.PostAsJsonAsync("/accounts", new { ownerName, currency });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> DepositAsync(this HttpClient client, Guid accountId, long amountMinor, string currency = "TRY") =>
        client.PostAsJsonAsync($"/accounts/{accountId}/deposits", new { amountMinor, currency, description = "Maaş yatırma" });

    public static Task<HttpResponseMessage> WithdrawAsync(this HttpClient client, Guid accountId, long amountMinor, string currency = "TRY") =>
        client.PostAsJsonAsync($"/accounts/{accountId}/withdrawals", new { amountMinor, currency, description = "ATM çekimi" });

    public static Task<HttpResponseMessage> TransferAsync(
        this HttpClient client, Guid fromAccountId, Guid toAccountId, long amountMinor, string currency = "TRY", string? description = null) =>
        client.PostAsJsonAsync("/transfers", new { fromAccountId, toAccountId, amountMinor, currency, description });

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
}

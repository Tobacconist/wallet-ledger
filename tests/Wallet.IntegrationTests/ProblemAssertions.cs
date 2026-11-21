using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wallet.IntegrationTests;

internal static class ProblemAssertions
{
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        if (code is not null)
        {
            Assert.Equal(code, body.GetProperty("code").GetString());
            Assert.EndsWith("/problems/" + code, body.GetProperty("type").GetString(), StringComparison.Ordinal);
        }

        return body;
    }
}

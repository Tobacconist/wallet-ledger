namespace Wallet.Api.Errors;

internal static class ProblemTypes
{
    private const string BaseUri = "https://wallet-api.local/problems/";

    public static string For(string code) => BaseUri + code;
}

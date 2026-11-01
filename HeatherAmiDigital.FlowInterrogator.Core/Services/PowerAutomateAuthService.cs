// PowerAutomateAuthService.cs
//
// Handles sign-in to the Power Automate Management API, a completely separate service from
// Dataverse with its own authentication — the Dataverse connection XrmToolBox already manages
// doesn't cover it. Uses MSAL's interactive flow: a normal system browser sign-in window pops
// up once, and the token is then cached in memory and reused silently (including across
// repeated calls) until it expires.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public sealed class PowerAutomateAuthService : ITokenProvider
{
    // Well-known multi-tenant public client ID (Azure PowerShell). Supports the loopback
    // (http://localhost) redirect used by the interactive flow without needing our own app
    // registration.
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
    private const string Authority = "https://login.microsoftonline.com/organizations";
    private static readonly string[] Scopes = { "https://service.flow.microsoft.com/.default" };

    private readonly IPublicClientApplication _msalClient;

    public PowerAutomateAuthService()
    {
        _msalClient = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(Authority)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var accounts = await _msalClient.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account != null)
        {
            try
            {
                var silentResult = await _msalClient.AcquireTokenSilent(Scopes, account).ExecuteAsync();
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // Cached token missing/expired with no refresh available; fall through to interactive.
            }
        }

        // The system browser (not an embedded WebView), so no parent window handle is needed
        // and sign-in looks like a normal browser login rather than an unfamiliar popup.
        var interactiveResult = await _msalClient
            .AcquireTokenInteractive(Scopes)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync();

        return interactiveResult.AccessToken;
    }
}

// ITokenProvider.cs
//
// Supplies access tokens for the Power Automate Management API. Kept separate from the
// concrete MSAL implementation so anything that needs a token doesn't need to know how it
// was acquired.

using System.Threading.Tasks;

namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public interface ITokenProvider
{
    Task<string> GetAccessTokenAsync();
}

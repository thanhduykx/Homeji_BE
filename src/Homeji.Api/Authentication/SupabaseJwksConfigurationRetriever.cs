using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Homeji.Api.Authentication;

internal sealed class SupabaseJwksConfigurationRetriever
    : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    private readonly string _issuer;

    public SupabaseJwksConfigurationRetriever(string issuer)
    {
        _issuer = issuer;
    }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(retriever);

        var document = await retriever.GetDocumentAsync(address, cancellationToken);
        var keySet = new JsonWebKeySet(document);
        var configuration = new OpenIdConnectConfiguration { Issuer = _issuer };

        foreach (var signingKey in keySet.GetSigningKeys())
        {
            configuration.SigningKeys.Add(signingKey);
        }

        return configuration;
    }
}

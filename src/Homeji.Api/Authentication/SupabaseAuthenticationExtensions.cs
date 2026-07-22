using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Homeji.Application.IServices.Accounts;

namespace Homeji.Api.Authentication;

public static class SupabaseAuthenticationExtensions
{
    public static IServiceCollection AddSupabaseAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var projectUrl = configuration["Supabase:ProjectUrl"];
        if (!Uri.TryCreate(projectUrl, UriKind.Absolute, out var projectUri)
            || projectUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Supabase:ProjectUrl must be a valid HTTPS URL, for example https://project-ref.supabase.co.");
        }

        var audience = configuration["Supabase:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Supabase:Audience is required.");
        }

        var issuer = $"{projectUri.ToString().TrimEnd('/')}/auth/v1";
        var jwksEndpoint = $"{issuer}/.well-known/jwks.json";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = true;
                options.RefreshOnIssuerKeyNotFound = true;
                options.ConfigurationManager =
                    new ConfigurationManager<OpenIdConnectConfiguration>(
                        jwksEndpoint,
                        new SupabaseJwksConfigurationRetriever(issuer),
                        new HttpDocumentRetriever { RequireHttps = true });

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "email",
                    RoleClaimType = "role",
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidAudience = audience,
                    ValidIssuer = issuer,
                    ValidAlgorithms =
                    [
                        SecurityAlgorithms.EcdsaSha256,
                        SecurityAlgorithms.RsaSha256,
                    ],
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var issuedAtValue = context.Principal?.FindFirst("iat")?.Value;
                        if (!Guid.TryParse(subject, out var userId)
                            || !long.TryParse(issuedAtValue, out var issuedAtUnixSeconds))
                        {
                            context.Fail("Token không chứa thông tin phiên hợp lệ.");
                            return;
                        }

                        var revocations = context.HttpContext.RequestServices
                            .GetRequiredService<IUserSessionRevocationService>();
                        var revokedBefore = await revocations.GetRevokedBeforeAsync(
                            userId,
                            context.HttpContext.RequestAborted);
                        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnixSeconds);
                        if (revokedBefore.HasValue && issuedAt <= revokedBefore.Value)
                        {
                            context.Fail("Phiên đăng nhập đã bị quản trị viên kết thúc.");
                        }
                    },
                };
            });

        return services;
    }
}

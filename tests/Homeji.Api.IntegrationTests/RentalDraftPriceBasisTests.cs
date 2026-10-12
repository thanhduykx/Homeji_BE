using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class RentalDraftPriceBasisTests
{
    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("unknown", "unknown")]
    [InlineData("person", "person")]
    [InlineData("room", "room")]
    public async Task Valid_basis_binds_and_reaches_real_service_as_normalized_facts(string? basis, string expected)
    {
        var ai = new CapturingClient();
        using var factory = CreateFactory(ai);
        using var client = CreateClient(factory);
        var payload = CreatePayload();
        if (basis is not null) payload["priceBasis"] = basis;

        using var response = await client.PostAsJsonAsync(new Uri("/api/ai/rental-draft", UriKind.Relative), payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<RentalDraftResponseDto>();
        Assert.NotNull(draft);
        Assert.Contains("«Tìm người ở ghép»", draft.Title, StringComparison.Ordinal);
        Assert.Contains("25 m²", draft.Title, StringComparison.Ordinal);
        Assert.Contains("3.000.000", draft.Title, StringComparison.Ordinal);
        Assert.Contains(expected == "person" ? "đồng/người/tháng" : expected == "room" ? "đồng/phòng/tháng" : "chưa xác nhận giá theo phòng hay theo người", draft.Description, StringComparison.Ordinal);
        Assert.Equal(expected == "unknown", draft.Missing.Any(value => value.Contains("Đơn vị tiền thuê")));
        Assert.Equal(1, ai.Calls);
        using var facts = JsonDocument.Parse(ai.Prompt[(ai.Prompt.LastIndexOf("Catalog: ", StringComparison.Ordinal) + "Catalog: ".Length)..]);
        Assert.Equal(expected, facts.RootElement.GetProperty("priceBasis").GetString());
        Assert.True(facts.RootElement.GetProperty("rentKnown").GetBoolean());
        Assert.DoesNotContain("Linh Trung", ai.Prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Room")]
    [InlineData("person ")]
    [InlineData("monthly")]
    public async Task Invalid_basis_returns_field_validation_400_without_calling_provider(string basis)
    {
        var ai = new CapturingClient();
        using var factory = CreateFactory(ai);
        using var client = CreateClient(factory);
        var payload = CreatePayload();
        payload["priceBasis"] = basis;

        using var response = await client.PostAsJsonAsync(new Uri("/api/ai/rental-draft", UriKind.Relative), payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("priceBasis", out _));
        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData("{\"title\":\"Đã xác minh\",\"description\":\"Giá 1 đồng\"}")]
    [InlineData("{\"titleVariantId\":\"brief\",\"sections\":[]}")]
    public async Task Provider_free_prose_or_missing_facts_returns_dependency_error_not_a_draft(string output)
    {
        var ai = new CapturingClient(output);
        using var factory = CreateFactory(ai);
        using var client = CreateClient(factory);
        using var response = await client.PostAsJsonAsync(new Uri("/api/ai/rental-draft", UriKind.Relative), CreatePayload());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, ai.Calls);
        Assert.DoesNotContain("Giá 1 đồng", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static Dictionary<string, object> CreatePayload() => new()
    {
        ["typeLabel"] = "Tìm người ở ghép", ["address"] = "Linh Trung", ["rent"] = "3000000",
        ["area"] = "25", ["amenities"] = new[] { "Bếp" }, ["imageCount"] = 3,
    };

    private static WebApplicationFactory<Program> CreateFactory(CapturingClient ai) =>
        new HomejiApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatbotAiClient>();
            services.AddScoped<IChatbotAiClient>(_ => ai);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "DraftTest";
                options.DefaultChallengeScheme = "DraftTest";
            }).AddScheme<AuthenticationSchemeOptions, DraftAuthenticationHandler>("DraftTest", _ => { });
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
        });

    private sealed class CapturingClient(string? output = null) : IChatbotAiClient
    {
        public int Calls { get; private set; }
        public string Prompt { get; private set; } = "";

        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages,
            string latestUserMessage, CancellationToken cancellationToken = default)
        {
            Calls++;
            Prompt = latestUserMessage;
            return Task.FromResult(output ?? """{"titleVariantId":"brief","sections":[{"factId":"type","variantId":"direct"},{"factId":"address","variantId":"direct"},{"factId":"rent","variantId":"direct"},{"factId":"area","variantId":"direct"},{"factId":"amenities","variantId":"direct"},{"factId":"confirmation","variantId":"direct"}]}""");
        }
    }

    // TestServer-only identity; production authentication and repositories remain untouched.
    private sealed class DraftAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "11111111-1111-4111-8111-111111111111")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}

using System.Net;
using System.Net.Http.Json;

namespace Homeji.Api.IntegrationTests;

public sealed class RentalAssistantApiTests(HomejiApiFactory factory) : IClassFixture<HomejiApiFactory>
{
    [Theory]
    [InlineData("/api/rental-assistant/compare", "POST")]
    [InlineData("/api/rental-assistant/draft-preview", "POST")]
    [InlineData("/api/rental-assistant/admin-summary", "GET")]
    [InlineData("/api/chatbot/conversations/9cab846d-b35d-458b-9553-920011c15d8b", "DELETE")]
    public async Task NewEndpointsRequireAuthentication(string path, string method)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (method == "POST") request.Content = JsonContent.Create(new { postIds = new[] { Guid.NewGuid() }, notes = "Preview" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

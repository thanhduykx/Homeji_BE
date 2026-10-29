using Homeji.Api.Controllers;
using Homeji.Api.Realtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Homeji.Api.IntegrationTests;

public sealed class ChatMediaControllerTests
{
    [Fact]
    public void Missing_giphy_key_returns_unavailable()
    {
        var controller = CreateController(new ConfigurationBuilder().Build());
        var result = Assert.IsType<ObjectResult>(controller.GifConfig());
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void Browser_key_configuration_never_exposes_turn_shared_secret()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Giphy:ApiKey"] = "public-browser-key", ["VoiceCalls:TurnSecret"] = "private-turn-secret" }).Build();
        var controller = CreateController(configuration);
        var result = Assert.IsType<OkObjectResult>(controller.GifConfig());
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("public-browser-key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-turn-secret", json, StringComparison.Ordinal);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Missing_turn_configuration_is_unavailable_and_never_cached()
    {
        var configuration = new ConfigurationBuilder().Build();
        var controller = CreateController(configuration);
        using var client = new HttpClient();
        var result = Assert.IsType<OkObjectResult>(await controller.VoiceConfig(new VoiceIceService(client, configuration, TimeProvider.System), CancellationToken.None));
        Assert.False(Assert.IsType<VoiceIceConfiguration>(result.Value).Available);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
    }
    private static ChatMediaController CreateController(IConfiguration configuration) => new(configuration)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
}

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Homeji.Infrastructure.External;
namespace Homeji.Api.IntegrationTests.Infrastructure;
public sealed class AiListingReviewClientTests
{
    [Fact] public void Retry_after_is_never_shortened_and_daily_quota_stops_for_a_day()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(45));
        Assert.Equal(TimeSpan.FromMinutes(45), GeminiListingReviewClient.RetryDelay(response, "{}"));
        Assert.Equal(TimeSpan.FromMinutes(45), GeminiListingReviewClient.RetryDelay(response, "{\"error\":{\"details\":[{\"retryDelay\":\"1200s\"}]}}"));
        Assert.Equal(TimeSpan.FromDays(1), GeminiListingReviewClient.RetryDelay(response, "GenerateRequestsPerDay"));
        response.Headers.RetryAfter = null;
        Assert.Equal(TimeSpan.FromMinutes(30), GeminiListingReviewClient.RetryDelay(response, "{}"));
        Assert.Equal(TimeSpan.FromMinutes(20), GeminiListingReviewClient.RetryDelay(response, "{\"error\":{\"details\":[{\"retryDelay\":\"1200s\"}]}}"));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"decision\":\"clear\",\"category\":\"fraud\",\"reason\":\"x\",\"evidence\":\"\",\"confidence\":1}")]
    [InlineData("{\"decision\":\"block\",\"category\":\"invented\",\"reason\":\"x\",\"evidence\":\"x\",\"confidence\":1}")]
    [InlineData("{\"decision\":\"block\",\"category\":\"fraud\",\"reason\":\"x\",\"evidence\":\"x\",\"confidence\":99}")]
    public void Unvalidated_AI_output_cannot_trigger_actions(string text)
    {
        Assert.ThrowsAny<Exception>(() => GeminiListingReviewClient.Parse(text));
    }
    [Fact] public void Valid_structured_output_is_read_without_rendering_arbitrary_markup()
    {
        var result = GeminiListingReviewClient.Parse("{\"decision\":\"review\",\"category\":\"fraud\",\"reason\":\"Cần kiểm tra thông tin.\",\"evidence\":\"Trích từ tin\",\"confidence\":0.8}");
        Assert.Equal("review", result.Decision);
    }
}

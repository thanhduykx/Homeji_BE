using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.Services.AI;

namespace Homeji.Application.UnitTests.AI;

public sealed class RentalDraftServiceTests
{
    private static readonly RentalDraftRequestDto Facts = new("Phòng trống", "Linh Trung", "3000000", "25", ["Bếp"], 2);

    [Fact]
    public async Task Draft_ComesFromGeminiAndKeepsMissingFeesExplicit()
    {
        var ai = new Client("""{"title":"Phòng Linh Trung","description":"Phòng có bếp theo thông tin chủ tin."}""");
        var result = await new RentalDraftService(ai).GenerateAsync(Facts);
        Assert.Equal(1, ai.Calls);
        Assert.Equal("Phòng Linh Trung", result.Title);
        Assert.Contains("3000000", ai.Prompt);
        Assert.Contains("Không phân tích ảnh", ai.Prompt);
        Assert.Contains(result.Missing, value => value.Contains("đơn giá điện"));
        Assert.Contains(result.Missing, value => value.Contains("3 ảnh"));
    }

    [Fact]
    public async Task InvalidInput_DoesNotCallProvider()
    {
        var ai = new Client("{}");
        await Assert.ThrowsAsync<RequestValidationException>(() => new RentalDraftService(ai)
            .GenerateAsync(Facts with { Amenities = [new string('x', 81)] }));
        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"title\":\"\",\"description\":\"\"}")]
    public async Task MalformedOutput_DoesNotFallBackToTemplate(string output)
    {
        await Assert.ThrowsAsync<ExternalDependencyException>(() => new RentalDraftService(new Client(output)).GenerateAsync(Facts));
    }

    [Fact]
    public async Task ProviderFailure_IsDisclosed()
    {
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => new RentalDraftService(new Client(null)).GenerateAsync(Facts));
    }

    private sealed class Client(string? reply) : IChatbotAiClient
    {
        public int Calls { get; private set; }
        public string Prompt { get; private set; } = "";
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string latestUserMessage, CancellationToken cancellationToken = default)
        {
            Calls++; Prompt = latestUserMessage;
            if (reply is null) throw new ExternalServiceUnavailableException("Gemini", "Unavailable");
            return Task.FromResult(reply);
        }
    }
}

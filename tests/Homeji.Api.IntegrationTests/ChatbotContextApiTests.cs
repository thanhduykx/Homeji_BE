using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class ChatbotContextApiTests
{
    [Fact]
    public async Task FoodOriginSurvivesHttpBindingAndFoodResponseIsAdditive()
    {
        var service = new CapturingChatbot();
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync(new Uri("/api/chatbot/messages", UriKind.Relative),
            new { message = "Gợi ý đồ ăn gần tui", foodContext = new { latitude = 10.84m, longitude = 106.8m, radiusKm = 3 } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ChatbotFoodContextDto(10.84m, 106.8m), service.LastRequest!.FoodContext);
        Assert.Null(service.LastRequest.RentalContext); Assert.Null(service.LastRequest.RentalSelection);
        var reply = await response.Content.ReadFromJsonAsync<ChatbotReplyDto>();
        Assert.Equal("empty", reply!.FoodDiscovery!.Status); Assert.Null(reply.SearchUpdate); Assert.Null(reply.RentalDecision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContextIsOptionalAndSurvivesHttpBindingAndViewMapping(bool withContext)
    {
        var service = new CapturingChatbot();
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var postId = Guid.NewGuid();
        object request = withContext
            ? new { message = "Tính tiền tháng", rentalContext = new { postId, task = "cost" } }
            : new { message = "Xin chào" };

        using var response = await client.PostAsJsonAsync(new Uri("/api/chatbot/messages", UriKind.Relative), request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(withContext ? new ChatbotRentalContextDto(postId, "cost") : null, service.LastRequest!.RentalContext);
        var reply = await response.Content.ReadFromJsonAsync<ChatbotReplyDto>();
        Assert.NotNull(reply);
        if (withContext) Assert.Equal(postId, Assert.Single(reply.RentalDecision!.UnavailablePostIds));
        else Assert.Null(reply.RentalDecision);
    }

    [Fact]
    public async Task MalformedContextIdReturns400BeforeServiceRuns()
    {
        var service = new CapturingChatbot();
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync(new Uri("/api/chatbot/messages", UriKind.Relative),
            new { message = "Tính tiền tháng", rentalContext = new { postId = "not-a-guid", task = "cost" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task SelectionAndScenarioSurviveHttpBindingWithoutChangingSingleRoomContract()
    {
        var service = new CapturingChatbot();
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        using var response = await client.PostAsJsonAsync(new Uri("/api/chatbot/messages", UriKind.Relative),
            new { message = "Ba lựa chọn này", rentalSelection = new { postIds = ids, task = "compare",
                scenario = new { occupants = 2, electricityKwh = 120, electricityUnit = "kwh" } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(service.LastRequest!.RentalContext);
        Assert.Equal(ids, service.LastRequest.RentalSelection!.PostIds);
        Assert.Equal("compare", service.LastRequest.RentalSelection.Task);
        Assert.Equal(2, service.LastRequest.RentalSelection.Scenario!.Occupants);
        Assert.Equal(120, service.LastRequest.RentalSelection.Scenario.ElectricityKwh);
        Assert.Equal("kwh", service.LastRequest.RentalSelection.Scenario.ElectricityUnit);
    }

    [Fact]
    public async Task MalformedSelectionIdReturns400BeforeServiceRuns()
    {
        var service = new CapturingChatbot();
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync(new Uri("/api/chatbot/messages", UriKind.Relative),
            new { message = "Hai lựa chọn này", rentalSelection = new { postIds = new[] { Guid.NewGuid().ToString(), "not-a-guid" } } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastRequest);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory(CapturingChatbot service) =>
        new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatbotService>();
                services.AddSingleton<IChatbotService>(service);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "ChatbotContextTest";
                    options.DefaultChallengeScheme = "ChatbotContextTest";
                }).AddScheme<AuthenticationSchemeOptions, ContextAuthenticationHandler>("ChatbotContextTest", _ => { });
            });
        });

    private sealed class ContextAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "11111111-1111-4111-8111-111111111111")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class CapturingChatbot : IChatbotService
    {
        public SendChatbotMessageDto? LastRequest { get; private set; }
        public Task<ChatbotReplyDto> SendMessageAsync(SendChatbotMessageDto request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var id = Guid.NewGuid();
            var reply = new ChatbotReplyDto(id, new(Guid.NewGuid(), id, ChatMessageSender.User, request.Message!, DateTimeOffset.UtcNow),
                new(Guid.NewGuid(), id, ChatMessageSender.Assistant, "Reply", DateTimeOffset.UtcNow), null, [])
            {
                RentalDecision = request.RentalContext is { } context ? new RentalDecisionResponseDto([], [context.PostId], "Unavailable") : null,
                FoodDiscovery = request.FoodContext is not null ? new("empty", "Chưa có món", [], Origin: request.FoodContext) : null,
            };
            return Task.FromResult(reply);
        }
        public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatbotPopupConfigDto> GetPopupConfigAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatbotConversationDto>> GetMyConversationsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatbotMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

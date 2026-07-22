using System.Text.Json;
using Homeji.Api.ErrorHandling;
using Homeji.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Homeji.Api.IntegrationTests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task ForbiddenAccessException_ReturnsForbiddenProblemDetails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var provider = services.BuildServiceProvider();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<ILogger<GlobalExceptionHandler>>(),
            provider.GetRequiredService<IProblemDetailsService>(),
            new TestHostEnvironment(),
            new ConfigurationBuilder().Build());

        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        context.Request.Path = "/api/saved-posts/22222222-2222-2222-2222-222222222222/roommate-candidates";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new ForbiddenAccessException("Hãy lưu tin đăng này trước khi xem ứng viên ở ghép."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: CancellationToken.None);
        Assert.Equal("Không có quyền truy cập", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "Hãy lưu tin đăng này trước khi xem ứng viên ở ghép.",
            body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ExternalServiceUnavailableException_ReturnsServiceUnavailableWithRetryAfter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var provider = services.BuildServiceProvider();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<ILogger<GlobalExceptionHandler>>(),
            provider.GetRequiredService<IProblemDetailsService>(),
            new TestHostEnvironment(),
            new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        context.Request.Path = "/api/chatbot/messages";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new ExternalServiceUnavailableException(
                "Gemini",
                "Chatbot tạm hết hạn mức. Vui lòng thử lại sau.",
                TimeSpan.FromSeconds(15)),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("15", context.Response.Headers.RetryAfter);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: CancellationToken.None);
        Assert.Equal("Dịch vụ bên ngoài tạm thời không khả dụng", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Gemini", body.RootElement.GetProperty("service").GetString());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Homeji.Api.IntegrationTests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

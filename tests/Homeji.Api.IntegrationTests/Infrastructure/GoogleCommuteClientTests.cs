using System.Net;
using System.Text.Json;
using Homeji.Application.DTOs.AI;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class GoogleCommuteClientTests
{
    [Fact]
    public async Task PartialErrorsRemainUnknownAndRequestsUseRealMatrixMode()
    {
        var handler = new MatrixHandler();
        var client = new GoogleCommuteClient(new HttpClient(handler), Options.Create(new GoogleCommuteOptions { Enabled = true, ApiKey = "test" }), TimeProvider.System);
        var first = new CommuteOriginDto(Guid.NewGuid(), 10.8m, 106.8m);
        var second = new CommuteOriginDto(Guid.NewGuid(), 10.81m, 106.81m);
        var result = await client.ComputeAsync([first, second], new("Selected campus", 10.85m, 106.85m, "DRIVE"));
        Assert.Equal(10m, result.Single(item => item.PostId == first.PostId).DurationMinutes);
        Assert.Null(result.Single(item => item.PostId == second.PostId).DurationMinutes);
        Assert.Equal("unknown", result.Single(item => item.PostId == second.PostId).Status);
        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal(2, payload.RootElement.GetProperty("origins").GetArrayLength());
        Assert.Single(payload.RootElement.GetProperty("destinations").EnumerateArray());
        Assert.Equal("DRIVE", payload.RootElement.GetProperty("travelMode").GetString());
        Assert.True(payload.RootElement.TryGetProperty("departureTime", out _));
        Assert.Contains("status", handler.FieldMask!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledFeatureDoesNotSendLocations()
    {
        var handler = new MatrixHandler();
        var client = new GoogleCommuteClient(new HttpClient(handler), Options.Create(new GoogleCommuteOptions()), TimeProvider.System);
        var result = await client.ComputeAsync([new(Guid.NewGuid(), 10.8m, 106.8m)], new("Campus", 10.85m, 106.85m));
        Assert.Null(handler.Body);
        Assert.Null(Assert.Single(result).DurationMinutes);
    }

    private sealed class MatrixHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? FieldMask { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            FieldMask = string.Join(',', request.Headers.GetValues("X-Goog-FieldMask"));
            return new(HttpStatusCode.OK) { Content = new StringContent("""[{"originIndex":1,"destinationIndex":0,"status":{"code":5},"condition":"ROUTE_NOT_FOUND"},{"originIndex":0,"destinationIndex":0,"status":{},"condition":"ROUTE_EXISTS","distanceMeters":1200,"duration":"600s"}]""") };
        }
    }
}

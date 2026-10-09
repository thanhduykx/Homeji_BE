using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class GoogleCommuteOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public int MaxOrigins { get; set; } = 10;
}

public sealed class GoogleCommuteClient(HttpClient httpClient, IOptions<GoogleCommuteOptions> options,
    TimeProvider timeProvider) : ICommuteClient
{
    public async Task<IReadOnlyCollection<CommuteEstimateDto>> ComputeAsync(IReadOnlyCollection<CommuteOriginDto> origins,
        CommuteDestinationDto destination, CancellationToken cancellationToken = default)
    {
        var items = origins.Take(Math.Clamp(options.Value.MaxOrigins, 1, 10)).ToArray();
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? departure = destination.Mode == "DRIVE" ? destination.DepartureTime ?? now.AddMinutes(1) : null;
        var results = origins.ToDictionary(origin => origin.PostId, origin => new CommuteEstimateDto(origin.PostId,
            null, null, destination.Mode, now, departure, "unknown"));
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.ApiKey) || items.Length == 0) return results.Values.ToArray();
        object Waypoint(decimal latitude, decimal longitude) => new { waypoint = new { location = new { latLng = new { latitude, longitude } } } };
        var payload = new Dictionary<string, object>
        {
            ["origins"] = items.Select(origin => Waypoint(origin.Latitude, origin.Longitude)).ToArray(),
            ["destinations"] = new[] { Waypoint(destination.Latitude, destination.Longitude) },
            ["travelMode"] = destination.Mode,
        };
        if (destination.Mode == "DRIVE")
        {
            payload["routingPreference"] = "TRAFFIC_AWARE";
            payload["departureTime"] = departure!.Value.ToString("O", CultureInfo.InvariantCulture);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix")
        { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-Goog-Api-Key", options.Value.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "originIndex,destinationIndex,status,condition,distanceMeters,duration");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return results.Values.ToArray();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            foreach (var element in json.RootElement.EnumerateArray())
            {
                var index = element.TryGetProperty("originIndex", out var indexValue) ? indexValue.GetInt32() : 0;
                if (index < 0 || index >= items.Length
                    || !element.TryGetProperty("condition", out var condition) || condition.GetString() != "ROUTE_EXISTS"
                    || element.TryGetProperty("status", out var status) && status.TryGetProperty("code", out var code) && code.GetInt32() != 0
                    || !element.TryGetProperty("duration", out var duration)
                    || !decimal.TryParse(duration.GetString()?.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    || seconds < 0 || !element.TryGetProperty("distanceMeters", out var distance) || !distance.TryGetInt32(out var meters) || meters < 0) continue;
                var id = items[index].PostId;
                results[id] = new(id, meters, seconds / 60m, destination.Mode, now, departure, "ok");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException)
        { /* Individual/unavailable routes remain unknown, never zero. No raw provider content is logged. */ }
        return results.Values.ToArray();
    }
}

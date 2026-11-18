using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Homeji.Api.Observability;

internal static class TelemetryRegistration
{
    public static IServiceCollection AddHomejiTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!configuration.GetValue("Telemetry:Enabled", true))
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(configuration["OTEL_SERVICE_NAME"] ?? "homeji-api")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = environment.EnvironmentName
                }))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter("Microsoft.AspNetCore.Hosting", "System.Runtime", "Homeji.AiSearch", "Homeji.RentalPublications")
                    .AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 10],
                        TagKeys = ["http.route", "http.request.method", "http.response.status_code"]
                    });
                if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}

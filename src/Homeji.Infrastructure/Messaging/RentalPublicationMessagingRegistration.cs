using Confluent.Kafka;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.Services.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.Messaging;

public static class RentalPublicationMessagingRegistration
{
    public static bool IsRentalPublicationWorkerEnabled(this IConfiguration configuration) =>
        configuration.GetValue<bool>($"{RentalPublicationKafkaOptions.Section}:Enabled");

    public static bool IsRentalPublicationAnalyticsEnabled(this IConfiguration configuration) =>
        configuration.GetValue<bool>($"{RentalPublicationKafkaOptions.Section}:AnalyticsEnabled");

    internal static IServiceCollection AddRentalPublicationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RentalPublicationKafkaOptions>()
            .Bind(configuration.GetSection(RentalPublicationKafkaOptions.Section))
            .Validate(options => options.IsValid(), "Kafka rental publications require valid broker, topic, group and security settings.")
            .ValidateOnStart();
        services.AddScoped<IRentalPublicationStore, RentalPublicationStore>();
        services.AddScoped<IAdminPlatformReader, AdminPlatformReader>();
        services.AddScoped<IRentalPublicationDispatcher, RentalPublicationDispatcher>();
        services.AddSingleton<IRentalPublicationAnalytics, RentalPublicationAnalytics>();
        services.AddSingleton<IProducer<string, string>>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RentalPublicationKafkaOptions>>().Value;
            var config = new ProducerConfig(options.CreateClientConfig())
            {
                EnableIdempotence = true,
                MessageTimeoutMs = 10_000,
                AllowAutoCreateTopics = false,
                ClientId = "homeji-rental-publications"
            };
            return new ProducerBuilder<string, string>(config).Build();
        });
        return services;
    }
}

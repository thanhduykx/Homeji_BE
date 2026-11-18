using Confluent.Kafka;

namespace Homeji.Infrastructure.Messaging;

internal sealed class RentalPublicationKafkaOptions
{
    public const string Section = "Kafka:RentalPublications";

    public bool Enabled { get; set; } = true;
    public bool AnalyticsEnabled { get; set; } = true;
    public string AnalyticsGroupId { get; set; } = "homeji.rental-publications.analytics.v1";
    public string? BootstrapServers { get; set; }
    public string Topic { get; set; } = "homeji.rental-publications.v1";
    public SecurityProtocol SecurityProtocol { get; set; }
    public SaslMechanism? SaslMechanism { get; set; }
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    public bool HasBroker => !string.IsNullOrWhiteSpace(BootstrapServers);

    public bool IsValid() => (!Enabled && !AnalyticsEnabled) || !HasBroker ||
        (Topic.Length is > 0 and <= 249 &&
         Topic is not "." and not ".." &&
         Topic.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-') &&
         (!AnalyticsEnabled || (!string.IsNullOrWhiteSpace(AnalyticsGroupId) && AnalyticsGroupId.Length <= 255)) &&
         (SecurityProtocol is not (SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext) ||
          (SaslMechanism is Confluent.Kafka.SaslMechanism.Plain or Confluent.Kafka.SaslMechanism.ScramSha256 or Confluent.Kafka.SaslMechanism.ScramSha512 &&
           !string.IsNullOrWhiteSpace(SaslUsername) && !string.IsNullOrWhiteSpace(SaslPassword))));

    public ClientConfig CreateClientConfig() => new()
    {
        BootstrapServers = BootstrapServers,
        SecurityProtocol = SecurityProtocol,
        SaslMechanism = SaslMechanism,
        SaslUsername = SaslUsername,
        SaslPassword = SaslPassword
    };
}

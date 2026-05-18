namespace DemoWeb.Options;

public sealed class CdcDemoOptions
{
    public const string SectionName = "CdcDemo";

    public string SourceConnectionString { get; set; } =
        "Server=localhost;Port=3306;Database=inventory;User ID=root;Password=debezium;";

    public string ReplicaConnectionString { get; set; } =
        "Server=localhost;Port=3306;Database=inventory;User ID=root;Password=debezium;";

    public string KafkaBootstrapServers { get; set; } = "localhost:9092";

    public string CustomerTopic { get; set; } = "mysql-server-1.inventory.customers";

    public string DeadLetterTopic { get; set; } = "cdc.dead-letter";

    public string KafkaConnectBaseUrl { get; set; } = "http://localhost:8083";

    public string ConnectorName { get; set; } = "inventory-connector";

    public int RecentMessageLimit { get; set; } = 12;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceConnectionString))
        {
            throw new InvalidOperationException("CdcDemo:SourceConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(ReplicaConnectionString))
        {
            throw new InvalidOperationException("CdcDemo:ReplicaConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(KafkaBootstrapServers))
        {
            throw new InvalidOperationException("CdcDemo:KafkaBootstrapServers is required.");
        }

        if (string.IsNullOrWhiteSpace(CustomerTopic))
        {
            throw new InvalidOperationException("CdcDemo:CustomerTopic is required.");
        }

        if (string.IsNullOrWhiteSpace(DeadLetterTopic))
        {
            throw new InvalidOperationException("CdcDemo:DeadLetterTopic is required.");
        }

        if (string.IsNullOrWhiteSpace(KafkaConnectBaseUrl))
        {
            throw new InvalidOperationException("CdcDemo:KafkaConnectBaseUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(ConnectorName))
        {
            throw new InvalidOperationException("CdcDemo:ConnectorName is required.");
        }

        if (RecentMessageLimit < 1)
        {
            throw new InvalidOperationException("CdcDemo:RecentMessageLimit must be greater than zero.");
        }
    }
}

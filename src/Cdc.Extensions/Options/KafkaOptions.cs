namespace Cdc.Extensions.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "";

    public string GroupId { get; set; } = "";

    public string DeadLetterTopic { get; set; } = "cdc.dead-letter";

    public int RetryDelaySeconds { get; set; } = 3;

    public int MaxProcessingAttempts { get; set; } = 3;

    public bool EnableDeadLetterTopic { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapServers))
        {
            throw new InvalidOperationException("Kafka:BootstrapServers is required.");
        }

        if (string.IsNullOrWhiteSpace(GroupId))
        {
            throw new InvalidOperationException("Kafka:GroupId is required.");
        }

        if (EnableDeadLetterTopic && string.IsNullOrWhiteSpace(DeadLetterTopic))
        {
            throw new InvalidOperationException("Kafka:DeadLetterTopic is required when Kafka:EnableDeadLetterTopic is true.");
        }

        if (RetryDelaySeconds < 1)
        {
            throw new InvalidOperationException("Kafka:RetryDelaySeconds must be greater than zero.");
        }

        if (MaxProcessingAttempts < 1)
        {
            throw new InvalidOperationException("Kafka:MaxProcessingAttempts must be greater than zero.");
        }
    }
}

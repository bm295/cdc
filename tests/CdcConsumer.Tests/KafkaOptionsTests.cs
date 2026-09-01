using CdcConsumer.Options;
using Xunit;

namespace CdcConsumer.Tests;

public sealed class KafkaOptionsTests
{
    [Fact]
    public void Validate_DeadLetterEnabledWithoutDeadLetterTopic_Throws()
    {
        var options = ValidOptions();
        options.DeadLetterTopic = " ";

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal(
            "Kafka:DeadLetterTopic is required when Kafka:EnableDeadLetterTopic is true.",
            exception.Message);
    }

    [Fact]
    public void Validate_DeadLetterDisabledWithoutDeadLetterTopic_DoesNotThrow()
    {
        var options = ValidOptions();
        options.EnableDeadLetterTopic = false;
        options.DeadLetterTopic = " ";

        options.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveRetryDelay_Throws(int retryDelaySeconds)
    {
        var options = ValidOptions();
        options.RetryDelaySeconds = retryDelaySeconds;

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Kafka:RetryDelaySeconds must be greater than zero.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveMaxProcessingAttempts_Throws(int maxProcessingAttempts)
    {
        var options = ValidOptions();
        options.MaxProcessingAttempts = maxProcessingAttempts;

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Kafka:MaxProcessingAttempts must be greater than zero.", exception.Message);
    }

    private static KafkaOptions ValidOptions()
    {
        return new KafkaOptions
        {
            BootstrapServers = "kafka:29092",
            Topic = "mysql-server-1.inventory.customers",
            GroupId = "cdc-consumer-group",
            DeadLetterTopic = "cdc.dead-letter",
            RetryDelaySeconds = 3,
            MaxProcessingAttempts = 3,
            EnableDeadLetterTopic = true
        };
    }
}

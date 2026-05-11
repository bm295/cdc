using System.Text.Json;
using CdcConsumer.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CdcConsumer.Infrastructure.Kafka;

public sealed class KafkaDeadLetterProducer : IDeadLetterProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions _options;
    private readonly IProducer<string, string> _producer;

    public KafkaDeadLetterProducer(IOptions<KafkaOptions> options)
    {
        _options = options.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = "cdc-consumer-dlq"
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken)
    {
        var key = message.Key ?? $"{message.Topic}:{message.Partition}:{message.Offset}";
        var value = JsonSerializer.Serialize(message, JsonOptions);

        await _producer.ProduceAsync(
            _options.DeadLetterTopic,
            new Message<string, string>
            {
                Key = key,
                Value = value
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}

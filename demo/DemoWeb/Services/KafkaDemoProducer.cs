using System.Text.Json;
using DemoWeb.Models;
using DemoWeb.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace DemoWeb.Services;

public sealed class KafkaDemoProducer(IOptions<CdcDemoOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CdcDemoOptions _options = options.Value;

    public async Task<DemoActionResponse> PublishPoisonMessageAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var poisonId = $"demo-poison-{startedAt.ToUnixTimeMilliseconds()}";

        var value = JsonSerializer.Serialize(
            new
            {
                before = (object?)null,
                after = new
                {
                    id = -1,
                    first_name = "Poison",
                    last_name = "Message",
                    email = "poison@example.local"
                },
                source = new
                {
                    db = "inventory",
                    table = "customers"
                },
                op = "x",
                ts_ms = startedAt.ToUnixTimeMilliseconds()
            },
            JsonOptions);

        var config = new ProducerConfig
        {
            BootstrapServers = _options.KafkaBootstrapServers,
            ClientId = "cdc-demo-web",
            MessageTimeoutMs = 5000
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        var result = await producer.ProduceAsync(
            _options.CustomerTopic,
            new Message<string, string>
            {
                Key = poisonId,
                Value = value
            },
            cancellationToken);

        return new DemoActionResponse(
            "Publish poison event",
            "submitted",
            $"PRODUCE {_options.CustomerTopic} op=x",
            $"Published unsupported Debezium operation at {result.TopicPartitionOffset}. The worker should retry it and send it to the DLQ.",
            null,
            startedAt);
    }
}

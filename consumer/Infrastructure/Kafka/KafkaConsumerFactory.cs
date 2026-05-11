using CdcConsumer.Options;
using Confluent.Kafka;

namespace CdcConsumer.Infrastructure.Kafka;

public sealed class KafkaConsumerFactory : IKafkaConsumerFactory
{
    public IConsumer<string, string> Create(KafkaOptions options)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            ClientId = "cdc-consumer"
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }
}

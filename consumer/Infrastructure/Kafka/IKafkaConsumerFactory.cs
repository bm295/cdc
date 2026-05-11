using CdcConsumer.Options;
using Confluent.Kafka;

namespace CdcConsumer.Infrastructure.Kafka;

public interface IKafkaConsumerFactory
{
    IConsumer<string, string> Create(KafkaOptions options);
}

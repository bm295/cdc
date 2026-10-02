using Cdc.Extensions.Options;
using Confluent.Kafka;

namespace Cdc.Extensions.Infrastructure.Kafka;

public interface IKafkaConsumerFactory
{
    IConsumer<string, string> Create(KafkaOptions options);
}

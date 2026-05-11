namespace CdcConsumer.Infrastructure.Kafka;

public interface IKafkaConsumerLoop
{
    Task RunAsync(CancellationToken cancellationToken);
}

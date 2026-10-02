namespace Cdc.Extensions.Infrastructure.Kafka;

public interface IKafkaConsumerLoop
{
    Task RunAsync(CancellationToken cancellationToken);
}

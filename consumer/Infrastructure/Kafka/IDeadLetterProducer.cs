namespace CdcConsumer.Infrastructure.Kafka;

public interface IDeadLetterProducer
{
    Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken);
}

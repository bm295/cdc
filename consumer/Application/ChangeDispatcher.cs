using Microsoft.Extensions.Logging;

namespace CdcConsumer.Application;

public sealed class ChangeDispatcher(
    IDebeziumEnvelopeParser parser,
    IChangeHandler<CustomerRecord> customerHandler,
    ILogger<ChangeDispatcher> logger) : IChangeDispatcher
{
    public async Task DispatchAsync(ConsumedMessage message, CancellationToken cancellationToken)
    {
        var changeEvent = parser.Parse<CustomerRecord>(message);

        if (changeEvent.Operation is ChangeOperation.Tombstone)
        {
            logger.LogDebug(
                "Ignoring Debezium tombstone for {Topic}[{Partition}]@{Offset}.",
                message.Topic,
                message.Partition,
                message.Offset);
            return;
        }

        await customerHandler.HandleAsync(changeEvent, cancellationToken);
    }
}

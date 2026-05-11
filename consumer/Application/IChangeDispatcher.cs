namespace CdcConsumer.Application;

public interface IChangeDispatcher
{
    Task DispatchAsync(ConsumedMessage message, CancellationToken cancellationToken);
}

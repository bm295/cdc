using Microsoft.Extensions.DependencyInjection;

namespace Cdc.Extensions.Application;

public abstract class ChangeSubscription(string topic)
{
    public string Topic { get; } = topic;
    public abstract Task DispatchAsync(IServiceProvider services, ConsumedMessage message, CancellationToken cancellationToken);
}

internal sealed class ChangeSubscription<T, THandler>(string topic) : ChangeSubscription(topic)
    where THandler : class, IChangeHandler<T>
{
    public override Task DispatchAsync(IServiceProvider services, ConsumedMessage message, CancellationToken cancellationToken)
    {
        var change = services.GetRequiredService<IDebeziumEnvelopeParser>().Parse<T>(message);
        return change.Operation == ChangeOperation.Tombstone
            ? Task.CompletedTask
            : services.GetRequiredService<THandler>().HandleAsync(change, cancellationToken);
    }
}

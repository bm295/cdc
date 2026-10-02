using Microsoft.Extensions.DependencyInjection;

namespace Cdc.Extensions.Application;

public sealed class ChangeDispatcher(
    IEnumerable<ChangeSubscription> subscriptions,
    IServiceScopeFactory scopeFactory) : IChangeDispatcher
{
    private readonly IReadOnlyDictionary<string, ChangeSubscription> _routes =
        subscriptions.ToDictionary(x => x.Topic, StringComparer.Ordinal);

    public async Task DispatchAsync(ConsumedMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_routes.TryGetValue(message.Topic, out var subscription))
            throw new InvalidOperationException($"No CDC handler registered for topic '{message.Topic}'.");
        await using var scope = scopeFactory.CreateAsyncScope();
        await subscription.DispatchAsync(scope.ServiceProvider, message, cancellationToken);
    }
}

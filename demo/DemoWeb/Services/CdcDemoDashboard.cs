using DemoWeb.Models;
using DemoWeb.Options;
using Microsoft.Extensions.Options;

namespace DemoWeb.Services;

public sealed class CdcDemoDashboard(
    IOptions<CdcDemoOptions> options,
    MySqlDemoStore store,
    KafkaConnectClient connectClient,
    KafkaTopicInspector topicInspector,
    KafkaDemoProducer producer)
{
    private readonly CdcDemoOptions _options = options.Value;

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        var connectorTask = GuardAsync(
            () => connectClient.GetStatusAsync(cancellationToken),
            "Kafka Connect",
            new ConnectorStatusDto(
                _options.ConnectorName,
                "unavailable",
                null,
                Array.Empty<ConnectorTaskStatusDto>(),
                "Connector status could not be loaded."),
            warnings);

        var sourceCustomersTask = GuardAsync(
            () => store.GetSourceCustomersAsync(cancellationToken),
            "source table",
            Array.Empty<CustomerRow>(),
            warnings);

        var replicaCustomersTask = GuardAsync(
            () => store.GetReplicaCustomersAsync(cancellationToken),
            "replica table",
            Array.Empty<CustomerRow>(),
            warnings);

        var customerMessagesTask = GuardAsync(
            () => topicInspector.ReadRecentAsync(_options.CustomerTopic, _options.RecentMessageLimit, false, cancellationToken),
            _options.CustomerTopic,
            Array.Empty<TopicMessageDto>(),
            warnings);

        var deadLetterMessagesTask = GuardAsync(
            () => topicInspector.ReadRecentAsync(_options.DeadLetterTopic, _options.RecentMessageLimit, true, cancellationToken),
            _options.DeadLetterTopic,
            Array.Empty<TopicMessageDto>(),
            warnings);

        await Task.WhenAll(
            connectorTask,
            sourceCustomersTask,
            replicaCustomersTask,
            customerMessagesTask,
            deadLetterMessagesTask);

        return new DashboardSnapshot(
            DateTimeOffset.UtcNow,
            await connectorTask,
            await sourceCustomersTask,
            await replicaCustomersTask,
            await customerMessagesTask,
            await deadLetterMessagesTask,
            warnings);
    }

    public Task<DemoActionResponse> InsertCustomerAsync(
        DemoActionRequest request,
        CancellationToken cancellationToken)
    {
        return store.InsertCustomerAsync(request, cancellationToken);
    }

    public Task<DemoActionResponse> UpdateCustomerAsync(
        DemoActionRequest request,
        CancellationToken cancellationToken)
    {
        return store.UpdateCustomerAsync(request, cancellationToken);
    }

    public Task<DemoActionResponse> DeleteCustomerAsync(
        DemoActionRequest request,
        CancellationToken cancellationToken)
    {
        return store.DeleteCustomerAsync(request, cancellationToken);
    }

    public Task<DemoActionResponse> TruncateCustomersAsync(CancellationToken cancellationToken)
    {
        return store.TruncateCustomersAsync(cancellationToken);
    }

    public Task<DemoActionResponse> SeedCustomersAsync(CancellationToken cancellationToken)
    {
        return store.SeedCustomersAsync(cancellationToken);
    }

    public Task<DemoActionResponse> PublishPoisonMessageAsync(CancellationToken cancellationToken)
    {
        return producer.PublishPoisonMessageAsync(cancellationToken);
    }

    private static async Task<T> GuardAsync<T>(
        Func<Task<T>> operation,
        string label,
        T fallback,
        ICollection<string> warnings)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            lock (warnings)
            {
                warnings.Add($"{label}: {ex.Message}");
            }

            return fallback;
        }
    }
}

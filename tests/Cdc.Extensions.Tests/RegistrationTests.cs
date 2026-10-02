using Cdc.Extensions.Application;
using Cdc.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cdc.Extensions.Tests;

public sealed class RegistrationTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapServers"] = "localhost:9092", ["GroupId"] = "test"
        }).Build();

    [Fact]
    public async Task Dispatch_RoutesModelsAndDisposesEachAsyncScope()
    {
        var services = new ServiceCollection();
        var records = new Records();
        services.AddSingleton(records);
        services.AddScoped<ScopedDependency>();
        services.AddCdc(Configuration())
            .Subscribe<TestRecord, CustomerHandler>("customers")
            .Subscribe<Order, OrderHandler>("orders");
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = provider.GetRequiredService<IChangeDispatcher>();
        await dispatcher.DispatchAsync(Message("customers", "{\"op\":\"c\",\"after\":{\"id\":5}}"), default);
        await dispatcher.DispatchAsync(Message("customers", "{\"op\":\"u\",\"after\":{\"id\":6}}"), default);
        await dispatcher.DispatchAsync(Message("orders", "{\"op\":\"r\",\"after\":{\"number\":42}}"), default);
        Assert.Equal(new[] { 5, 6, 42 }, records.Values);
        Assert.Equal(3, records.Scopes.Distinct().Count());
        Assert.Equal(3, records.Disposed);
    }

    [Fact]
    public async Task Tombstone_DoesNotResolveHandler_UnknownTopicFails()
    {
        var services = new ServiceCollection();
        services.AddCdc(Configuration()).Subscribe<TestRecord, CustomerHandler>("customers");
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IChangeDispatcher>();
        await dispatcher.DispatchAsync(Message("customers", null), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(Message("unknown", null), default));
    }

    [Fact]
    public void DuplicateTopicAndDuplicateAddCdc_AreRejected()
    {
        var services = new ServiceCollection();
        var builder = services.AddCdc(Configuration()).Subscribe<TestRecord, CustomerHandler>("customers");
        Assert.Throws<ArgumentException>(() => builder.Subscribe<Order, OrderHandler>("customers"));
        Assert.Throws<InvalidOperationException>(() => services.AddCdc(Configuration()));
    }

    [Fact]
    public void MissingSubscriptionsOrBroker_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddCdc(Configuration());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<KafkaOptions>>().Value);
        var invalid = new KafkaOptions { GroupId = "test" };
        Assert.Throws<InvalidOperationException>(invalid.Validate);
    }

    private static ConsumedMessage Message(string topic, string? value) => new(topic, 0, 1, null, value);
    public sealed record Order(int Number);
    public sealed class Records
    {
        public List<int> Values { get; } = [];
        public List<Guid> Scopes { get; } = [];
        public int Disposed;
    }
    public sealed class ScopedDependency(Records records) : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public ValueTask DisposeAsync() { records.Disposed++; return ValueTask.CompletedTask; }
    }
    public sealed class CustomerHandler(ScopedDependency dependency, Records records) : IChangeHandler<TestRecord>
    {
        public Task HandleAsync(ChangeEvent<TestRecord> change, CancellationToken cancellationToken)
        {
            records.Values.Add(change.After!.Id); records.Scopes.Add(dependency.Id); return Task.CompletedTask;
        }
    }
    public sealed class OrderHandler(ScopedDependency dependency, Records records) : IChangeHandler<Order>
    {
        public Task HandleAsync(ChangeEvent<Order> change, CancellationToken cancellationToken)
        {
            records.Values.Add(change.After!.Number); records.Scopes.Add(dependency.Id); return Task.CompletedTask;
        }
    }
}

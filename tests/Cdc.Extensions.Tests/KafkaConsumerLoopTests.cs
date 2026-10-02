using System.Reflection;
using Cdc.Extensions.Application;
using Cdc.Extensions.Infrastructure.Kafka;
using Cdc.Extensions.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cdc.Extensions.Tests;

public sealed class KafkaConsumerLoopTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitFailure_DoesNotRetryHandlerOrPublishDeadLetter(bool commitFails)
    {
        using var stop = new CancellationTokenSource();
        var consumer = DispatchProxy.Create<IConsumer<string, string>, ConsumerProxy>();
        var proxy = (ConsumerProxy)(object)consumer;
        proxy.Stop = stop;
        proxy.CommitFails = commitFails;
        var dispatcher = new Dispatcher(_ => Task.CompletedTask);
        var dlq = new DeadLetters();
        await Run(consumer, dispatcher, dlq, stop);
        Assert.Equal(1, dispatcher.Calls);
        Assert.Equal(1, proxy.Commits);
        Assert.Equal(0, dlq.Calls);
        Assert.Equal(new[] { "customers", "orders" }, proxy.Topics);
    }

    [Fact]
    public async Task HandlerFailure_RetriesThenPublishesBeforeCommit()
    {
        using var stop = new CancellationTokenSource();
        var consumer = DispatchProxy.Create<IConsumer<string, string>, ConsumerProxy>();
        var proxy = (ConsumerProxy)(object)consumer;
        proxy.Stop = stop;
        var dlq = new DeadLetters();
        proxy.BeforeCommit = () => Assert.Equal(1, dlq.Calls);
        var dispatcher = new Dispatcher(_ => throw new InvalidDataException("bad message"));
        await Run(consumer, dispatcher, dlq, stop);
        Assert.Equal(2, dispatcher.Calls);
        Assert.Equal(1, proxy.Commits);
        Assert.Equal("customers", dlq.Message!.Topic);
        Assert.Equal(12, dlq.Message.Offset);
    }

    [Fact]
    public async Task FailedDeadLetter_DoesNotCommit()
    {
        using var stop = new CancellationTokenSource();
        var consumer = DispatchProxy.Create<IConsumer<string, string>, ConsumerProxy>();
        var proxy = (ConsumerProxy)(object)consumer;
        proxy.Stop = stop;
        var dlq = new DeadLetters { Failure = () => { stop.Cancel(); throw new IOException("DLQ unavailable"); } };
        await Run(consumer, new Dispatcher(_ => throw new InvalidDataException()), dlq, stop, attempts: 1);
        Assert.Equal(1, dlq.Calls);
        Assert.Equal(0, proxy.Commits);
    }

    [Fact]
    public async Task Cancellation_DoesNotRetryPublishOrCommit()
    {
        using var stop = new CancellationTokenSource();
        var consumer = DispatchProxy.Create<IConsumer<string, string>, ConsumerProxy>();
        var proxy = (ConsumerProxy)(object)consumer;
        proxy.Stop = stop;
        var dispatcher = new Dispatcher(ct => { stop.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        var dlq = new DeadLetters();
        await Run(consumer, dispatcher, dlq, stop);
        Assert.Equal(1, dispatcher.Calls);
        Assert.Equal(0, proxy.Commits);
        Assert.Equal(0, dlq.Calls);
    }

    [Fact]
    public async Task DisabledDeadLetter_DoesNotCommitExhaustedFailure()
    {
        using var stop = new CancellationTokenSource();
        var consumer = DispatchProxy.Create<IConsumer<string, string>, ConsumerProxy>();
        var proxy = (ConsumerProxy)(object)consumer;
        proxy.Stop = stop;
        var dlq = new DeadLetters();
        await Run(consumer, new Dispatcher(_ => { stop.Cancel(); throw new IOException(); }), dlq, stop,
            attempts: 1, enableDlq: false);
        Assert.Equal(0, proxy.Commits);
        Assert.Equal(0, dlq.Calls);
    }

    private static async Task Run(IConsumer<string, string> consumer, Dispatcher dispatcher,
        DeadLetters dlq, CancellationTokenSource stop, int attempts = 2, bool enableDlq = true)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new KafkaOptions
        {
            BootstrapServers = "test", GroupId = "test", RetryDelaySeconds = 1,
            MaxProcessingAttempts = attempts, EnableDeadLetterTopic = enableDlq
        });
        var loop = new KafkaConsumerLoop(options, new Factory(consumer), dispatcher, dlq,
            [new Subscription("customers"), new Subscription("orders")], NullLogger<KafkaConsumerLoop>.Instance);
        try { await loop.RunAsync(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public class ConsumerProxy : DispatchProxy
    {
        public CancellationTokenSource Stop = null!;
        public bool CommitFails;
        public int Commits;
        public string[] Topics = [];
        public Action? BeforeCommit;
        private bool _consumed;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "Subscribe": Topics = ((IEnumerable<string>)args![0]!).ToArray(); return null;
                case "Consume":
                    if (_consumed) { Stop.Cancel(); throw new OperationCanceledException(Stop.Token); }
                    _consumed = true;
                    return new ConsumeResult<string, string>
                    {
                        Topic = "customers", Partition = new Partition(0), Offset = new Offset(12),
                        Message = new Message<string, string> { Key = "1", Value = "{}" }
                    };
                case "Commit":
                    BeforeCommit?.Invoke(); Commits++;
                    if (CommitFails) { Stop.Cancel(); throw new IOException("commit failed"); }
                    return null;
                case "StoreOffset": case "Dispose": return null;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
    private sealed class Factory(IConsumer<string, string> consumer) : IKafkaConsumerFactory
    {
        public IConsumer<string, string> Create(KafkaOptions options) => consumer;
    }
    private sealed class Dispatcher(Func<CancellationToken, Task> action) : IChangeDispatcher
    {
        public int Calls;
        public Task DispatchAsync(ConsumedMessage message, CancellationToken cancellationToken)
        { Calls++; return action(cancellationToken); }
    }
    private sealed class DeadLetters : IDeadLetterProducer
    {
        public int Calls;
        public DeadLetterMessage? Message;
        public Action? Failure;
        public Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken)
        { Calls++; Message = message; Failure?.Invoke(); return Task.CompletedTask; }
    }
    private sealed class Subscription(string topic) : ChangeSubscription(topic)
    {
        public override Task DispatchAsync(IServiceProvider services, ConsumedMessage message, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

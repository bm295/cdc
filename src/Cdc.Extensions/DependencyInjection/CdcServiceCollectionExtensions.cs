using Cdc.Extensions.Application;
using Cdc.Extensions.Infrastructure.Kafka;
using Cdc.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cdc.Extensions;

public static class CdcServiceCollectionExtensions
{
    public static CdcBuilder AddCdc(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(CdcBuilder)))
            throw new InvalidOperationException("AddCdc can only be called once per service collection.");
        services.AddOptions<KafkaOptions>().Bind(configuration)
            .Validate(options => { options.Validate(); return true; })
            .ValidateOnStart();
        services.AddOptions<KafkaOptions>()
            .Validate<IServiceProvider>((_, provider) => provider.GetServices<ChangeSubscription>().Any(),
                "At least one CDC topic subscription is required.");
        services.TryAddSingleton<IDebeziumEnvelopeParser, DebeziumEnvelopeParser>();
        services.TryAddSingleton<IChangeDispatcher, ChangeDispatcher>();
        services.TryAddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
        services.TryAddSingleton<IDeadLetterProducer, KafkaDeadLetterProducer>();
        services.TryAddSingleton<IKafkaConsumerLoop, KafkaConsumerLoop>();
        services.AddHostedService<CdcConsumerWorker>();
        var builder = new CdcBuilder(services);
        services.AddSingleton(builder);
        return builder;
    }
}

public sealed class CdcBuilder
{
    private readonly IServiceCollection _services;
    private readonly HashSet<string> _topics = new(StringComparer.Ordinal);
    internal CdcBuilder(IServiceCollection services) => _services = services;

    public CdcBuilder Subscribe<T, THandler>(string topic) where THandler : class, IChangeHandler<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (!_topics.Add(topic))
            throw new ArgumentException($"CDC topic '{topic}' is already registered.", nameof(topic));
        _services.TryAddScoped<THandler>();
        _services.AddSingleton<ChangeSubscription>(new ChangeSubscription<T, THandler>(topic));
        return this;
    }
}

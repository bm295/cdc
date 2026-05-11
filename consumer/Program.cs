using CdcConsumer;
using CdcConsumer.Application;
using CdcConsumer.Application.Customers;
using CdcConsumer.Infrastructure.Kafka;
using CdcConsumer.Infrastructure.ReplicaDb;
using CdcConsumer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection(KafkaOptions.SectionName));

builder.Services.PostConfigure<KafkaOptions>(options =>
{
    options.ApplyLegacyEnvironmentVariables();
    options.Validate();
});

builder.Services.Configure<ReplicaDbOptions>(
    builder.Configuration.GetSection(ReplicaDbOptions.SectionName));

builder.Services.PostConfigure<ReplicaDbOptions>(options =>
{
    options.Validate();
});

builder.Services.AddSingleton<IDebeziumEnvelopeParser, DebeziumEnvelopeParser>();
builder.Services.AddSingleton<IReplicaCustomerStore, MySqlReplicaCustomerStore>();
builder.Services.AddSingleton<IChangeHandler<CustomerRecord>, CustomerChangeHandler>();
builder.Services.AddSingleton<IChangeDispatcher, ChangeDispatcher>();
builder.Services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
builder.Services.AddSingleton<IDeadLetterProducer, KafkaDeadLetterProducer>();
builder.Services.AddSingleton<IKafkaConsumerLoop, KafkaConsumerLoop>();
builder.Services.AddHostedService<CdcConsumerWorker>();

await builder.Build().RunAsync();

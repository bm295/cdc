using CdcConsumer;
using CdcConsumer.Application;
using CdcConsumer.Application.Customers;
using CdcConsumer.Infrastructure.Kafka;
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

builder.Services.AddSingleton<IDebeziumEnvelopeParser, DebeziumEnvelopeParser>();
builder.Services.AddSingleton<IChangeHandler<CustomerRecord>, CustomerChangeHandler>();
builder.Services.AddSingleton<IChangeDispatcher, ChangeDispatcher>();
builder.Services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
builder.Services.AddSingleton<IDeadLetterProducer, KafkaDeadLetterProducer>();
builder.Services.AddSingleton<IKafkaConsumerLoop, KafkaConsumerLoop>();
builder.Services.AddHostedService<CdcConsumerWorker>();

await builder.Build().RunAsync();

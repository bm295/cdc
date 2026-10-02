using Microsoft.Extensions.Configuration;
using Cdc.Extensions;
using Cdc.Extensions.Options;
using CdcConsumer;
using CdcConsumer.Application.Customers;
using CdcConsumer.Infrastructure.ReplicaDb;
using CdcConsumer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Keep the demo's existing Kafka configuration and legacy environment variables.
builder.Services.AddCdc(builder.Configuration.GetSection("Kafka"))
    .Subscribe<CustomerRecord, CustomerChangeHandler>(
        Environment.GetEnvironmentVariable("TOPIC")
        ?? builder.Configuration["Kafka:Topic"]
        ?? "mysql-server-1.inventory.customers");
builder.Services.PostConfigure<KafkaOptions>(options =>
{
    options.BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? options.BootstrapServers;
    options.GroupId = Environment.GetEnvironmentVariable("GROUP_ID") ?? options.GroupId;
    options.DeadLetterTopic = Environment.GetEnvironmentVariable("DLQ_TOPIC") ?? options.DeadLetterTopic;
});
builder.Services.AddOptions<ReplicaDbOptions>()
    .Bind(builder.Configuration.GetSection(ReplicaDbOptions.SectionName))
    .Validate(options => { options.Validate(); return true; })
    .ValidateOnStart();
builder.Services.AddScoped<IReplicaCustomerStore, MySqlReplicaCustomerStore>();

await builder.Build().RunAsync();

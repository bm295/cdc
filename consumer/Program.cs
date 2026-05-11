using System.Text;
using Confluent.Kafka;

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:29092";
var topic = Environment.GetEnvironmentVariable("TOPIC") ?? "mysql-server-1.inventory.customers";

var config = new ConsumerConfig
{
    BootstrapServers = bootstrapServers,
    GroupId = "cdc-consumer-group",
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = true
};

while (true)
{
    try
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(topic);

        Console.WriteLine($"Listening CDC events from topic: {topic}");

        while (true)
        {
            var result = consumer.Consume(CancellationToken.None);
            Console.WriteLine(result.Message.Value);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Consumer waiting for Kafka... {ex.Message}");
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}

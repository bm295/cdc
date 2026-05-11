using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CdcConsumer.Infrastructure.Kafka;

public sealed class CdcConsumerWorker(
    IKafkaConsumerLoop consumerLoop,
    ILogger<CdcConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CDC consumer worker starting.");
        await consumerLoop.RunAsync(stoppingToken);
    }
}

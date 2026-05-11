using CdcConsumer.Application;
using CdcConsumer.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdcConsumer.Infrastructure.Kafka;

public sealed class KafkaConsumerLoop(
    IOptions<KafkaOptions> options,
    IKafkaConsumerFactory consumerFactory,
    IChangeDispatcher dispatcher,
    IDeadLetterProducer deadLetterProducer,
    ILogger<KafkaConsumerLoop> logger) : IKafkaConsumerLoop
{
    private readonly KafkaOptions _options = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = consumerFactory.Create(_options);
                consumer.Subscribe(_options.Topic);

                logger.LogInformation(
                    "Listening for CDC events on {Topic} with consumer group {GroupId}.",
                    _options.Topic,
                    _options.GroupId);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(cancellationToken);

                    if (result?.Message is null)
                    {
                        continue;
                    }

                    await ProcessMessageAsync(consumer, result, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Kafka consumer loop failed. Retrying in {RetryDelaySeconds} seconds.",
                    _options.RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxProcessingAttempts; attempt++)
        {
            try
            {
                await dispatcher.DispatchAsync(ToConsumedMessage(result), cancellationToken);
                CommitProcessedMessage(consumer, result);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxProcessingAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Failed processing {Topic}[{Partition}]@{Offset} on attempt {Attempt}/{MaxAttempts}.",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    attempt,
                    _options.MaxProcessingAttempts);

                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken);
            }
            catch (Exception ex)
            {
                await PublishDeadLetterAsync(result, ex, cancellationToken);
                CommitProcessedMessage(consumer, result);
                return;
            }
        }
    }

    private async Task PublishDeadLetterAsync(
        ConsumeResult<string, string> result,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableDeadLetterTopic)
        {
            throw new InvalidOperationException(
                "Message processing failed and the dead-letter topic is disabled.",
                exception);
        }

        var deadLetter = new DeadLetterMessage(
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            result.Message.Key,
            result.Message.Value,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.StackTrace,
            DateTimeOffset.UtcNow);

        await deadLetterProducer.PublishAsync(deadLetter, cancellationToken);

        logger.LogError(
            exception,
            "Sent {Topic}[{Partition}]@{Offset} to dead-letter topic {DeadLetterTopic}.",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            _options.DeadLetterTopic);
    }

    private static ConsumedMessage ToConsumedMessage(ConsumeResult<string, string> result)
    {
        return new ConsumedMessage(
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            result.Message.Key,
            result.Message.Value);
    }

    private static void CommitProcessedMessage(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
    {
        consumer.StoreOffset(result);
        consumer.Commit(result);
    }
}

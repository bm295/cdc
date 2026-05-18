using System.Text.Json;
using DemoWeb.Models;
using DemoWeb.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace DemoWeb.Services;

public sealed class KafkaTopicInspector(IOptions<CdcDemoOptions> options)
{
    private readonly CdcDemoOptions _options = options.Value;

    public Task<IReadOnlyList<TopicMessageDto>> ReadRecentAsync(
        string topic,
        int limit,
        bool deadLetter,
        CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.KafkaBootstrapServers,
            GroupId = $"cdc-demo-inspector-{Guid.NewGuid():N}",
            EnableAutoCommit = false,
            EnablePartitionEof = true,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _options.KafkaBootstrapServers
        }).Build();

        var messages = new List<TopicMessageDto>();

        var metadata = adminClient.GetMetadata(topic, TimeSpan.FromSeconds(3));
        var topicMetadata = metadata.Topics.FirstOrDefault(item => item.Topic == topic);

        if (topicMetadata is null || topicMetadata.Error.IsError)
        {
            return Task.FromResult<IReadOnlyList<TopicMessageDto>>(messages);
        }

        foreach (var partitionMetadata in topicMetadata.Partitions.OrderBy(item => item.PartitionId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (partitionMetadata.Error.IsError)
            {
                continue;
            }

            var topicPartition = new TopicPartition(topic, new Partition(partitionMetadata.PartitionId));
            WatermarkOffsets watermarks;

            try
            {
                watermarks = consumer.QueryWatermarkOffsets(topicPartition, TimeSpan.FromSeconds(3));
            }
            catch (KafkaException)
            {
                continue;
            }

            if (watermarks.High <= watermarks.Low)
            {
                continue;
            }

            var startOffset = Math.Max(watermarks.Low.Value, watermarks.High.Value - limit);
            consumer.Assign(new TopicPartitionOffset(topicPartition, new Offset(startOffset)));

            var partitionMessages = ReadPartitionMessages(
                consumer,
                watermarks.High.Value,
                limit,
                deadLetter,
                cancellationToken);

            messages.AddRange(partitionMessages);
        }

        return Task.FromResult<IReadOnlyList<TopicMessageDto>>(
            messages
                .OrderByDescending(message => message.KafkaTimestamp ?? DateTimeOffset.MinValue)
                .ThenByDescending(message => message.Offset)
                .Take(limit)
                .ToArray());
    }

    private static IReadOnlyList<TopicMessageDto> ReadPartitionMessages(
        IConsumer<string, string> consumer,
        long highWatermark,
        int limit,
        bool deadLetter,
        CancellationToken cancellationToken)
    {
        var messages = new List<TopicMessageDto>();
        var idlePolls = 0;

        while (messages.Count < limit && idlePolls < 6)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConsumeResult<string, string>? result;

            try
            {
                result = consumer.Consume(TimeSpan.FromMilliseconds(250));
            }
            catch (ConsumeException)
            {
                break;
            }

            if (result is null)
            {
                idlePolls++;
                continue;
            }

            if (result.IsPartitionEOF || result.Offset.Value >= highWatermark)
            {
                break;
            }

            idlePolls = 0;
            messages.Add(ToDto(result, deadLetter));
        }

        return messages;
    }

    private static TopicMessageDto ToDto(ConsumeResult<string, string> result, bool deadLetter)
    {
        var value = result.Message.Value;
        var key = result.Message.Key;
        var operation = default(string);
        var database = default(string);
        var table = default(string);
        var errorType = default(string);
        var errorMessage = default(string);
        var summary = string.IsNullOrWhiteSpace(value) ? "Tombstone message" : "Kafka message";

        if (!string.IsNullOrWhiteSpace(value))
        {
            if (deadLetter)
            {
                ParseDeadLetter(value, ref operation, ref database, ref table, ref errorType, ref errorMessage, ref summary);
            }
            else
            {
                ParseDebeziumEnvelope(value, ref operation, ref database, ref table, ref summary);
            }
        }

        return new TopicMessageDto(
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            key,
            operation,
            database,
            table,
            summary,
            value,
            errorType,
            errorMessage,
            ToTimestamp(result.Message.Timestamp));
    }

    private static void ParseDeadLetter(
        string value,
        ref string? operation,
        ref string? database,
        ref string? table,
        ref string? errorType,
        ref string? errorMessage,
        ref string summary)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;

            errorType = TryGetString(root, "errorType");
            errorMessage = TryGetString(root, "errorMessage");
            summary = errorMessage ?? errorType ?? "Dead-letter message";

            var originalValue = TryGetString(root, "value");

            if (!string.IsNullOrWhiteSpace(originalValue))
            {
                ParseDebeziumEnvelope(originalValue, ref operation, ref database, ref table, ref summary);
                summary = errorMessage ?? summary;
            }
        }
        catch (JsonException)
        {
            summary = "Dead-letter payload is not JSON";
        }
    }

    private static void ParseDebeziumEnvelope(
        string value,
        ref string? operation,
        ref string? database,
        ref string? table,
        ref string summary)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = UnwrapPayload(document.RootElement);

            if (root.ValueKind is JsonValueKind.Null)
            {
                summary = "Debezium tombstone payload";
                return;
            }

            operation = TryGetString(root, "op");

            if (root.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object)
            {
                database = TryGetString(source, "db");
                table = TryGetString(source, "table");
            }

            summary = operation switch
            {
                "c" => "Create event",
                "u" => "Update event",
                "d" => "Delete event",
                "r" => "Snapshot read event",
                "t" => "Truncate event",
                null or "" => "Debezium event",
                _ => $"Unsupported operation {operation}"
            };
        }
        catch (JsonException)
        {
            summary = "Message value is not JSON";
        }
    }

    private static JsonElement UnwrapPayload(JsonElement root)
    {
        return root.TryGetProperty("payload", out var payload)
            ? payload
            : root;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;
    }

    private static DateTimeOffset? ToTimestamp(Timestamp timestamp)
    {
        if (timestamp.Type is TimestampType.NotAvailable)
        {
            return null;
        }

        return new DateTimeOffset(timestamp.UtcDateTime);
    }
}

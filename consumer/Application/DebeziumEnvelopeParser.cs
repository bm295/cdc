using System.Text.Json;

namespace CdcConsumer.Application;

public sealed class DebeziumEnvelopeParser : IDebeziumEnvelopeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ChangeEvent<T> Parse<T>(ConsumedMessage message)
    {
        var metadata = new ChangeMetadata(
            message.Topic,
            message.Partition,
            message.Offset,
            message.Key);

        if (string.IsNullOrWhiteSpace(message.Value))
        {
            return new ChangeEvent<T>(
                metadata,
                ChangeOperation.Tombstone,
                default,
                default,
                default,
                default);
        }

        DebeziumEnvelope<T>? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<DebeziumEnvelope<T>>(message.Value, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("CDC event value is not valid Debezium JSON.", ex);
        }

        if (envelope is null)
        {
            throw new InvalidDataException("CDC event value could not be parsed as a Debezium envelope.");
        }

        var operation = ChangeOperationMapper.FromDebeziumCode(envelope.Operation);

        if (operation is ChangeOperation.Unknown)
        {
            throw new InvalidDataException($"CDC event contains unsupported Debezium operation '{envelope.Operation}'.");
        }

        return new ChangeEvent<T>(
            metadata,
            operation,
            envelope.Before,
            envelope.After,
            envelope.Source,
            ToDateTimeOffset(envelope.TimestampMilliseconds ?? envelope.Source?.TimestampMilliseconds));
    }

    private static DateTimeOffset? ToDateTimeOffset(long? milliseconds)
    {
        if (milliseconds is null)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException($"CDC event timestamp '{milliseconds}' is out of range.", ex);
        }
    }
}

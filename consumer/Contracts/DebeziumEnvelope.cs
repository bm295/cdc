using System.Text.Json.Serialization;

namespace CdcConsumer;

public sealed record DebeziumEnvelope<T>
{
    [JsonPropertyName("before")]
    public T? Before { get; init; }

    [JsonPropertyName("after")]
    public T? After { get; init; }

    [JsonPropertyName("source")]
    public DebeziumSource? Source { get; init; }

    [JsonPropertyName("op")]
    public string? Operation { get; init; }

    [JsonPropertyName("ts_ms")]
    public long? TimestampMilliseconds { get; init; }
}

public sealed record DebeziumSource
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("connector")]
    public string? Connector { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("db")]
    public string? Database { get; init; }

    [JsonPropertyName("table")]
    public string? Table { get; init; }

    [JsonPropertyName("file")]
    public string? File { get; init; }

    [JsonPropertyName("pos")]
    public long? Position { get; init; }

    [JsonPropertyName("row")]
    public int? Row { get; init; }

    [JsonPropertyName("snapshot")]
    public string? Snapshot { get; init; }

    [JsonPropertyName("ts_ms")]
    public long? TimestampMilliseconds { get; init; }
}

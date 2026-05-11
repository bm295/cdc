namespace CdcConsumer;

public sealed record ChangeEvent<T>(
    ChangeMetadata Metadata,
    ChangeOperation Operation,
    T? Before,
    T? After,
    DebeziumSource? Source,
    DateTimeOffset? OccurredAt);

public sealed record ChangeMetadata(
    string Topic,
    int Partition,
    long Offset,
    string? Key);

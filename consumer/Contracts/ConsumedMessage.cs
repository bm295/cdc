namespace CdcConsumer;

public sealed record ConsumedMessage(
    string Topic,
    int Partition,
    long Offset,
    string? Key,
    string? Value);

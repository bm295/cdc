namespace Cdc.Extensions.Infrastructure.Kafka;

public sealed record DeadLetterMessage(
    string Topic,
    int Partition,
    long Offset,
    string? Key,
    string? Value,
    string ErrorType,
    string ErrorMessage,
    string? StackTrace,
    DateTimeOffset FailedAt);

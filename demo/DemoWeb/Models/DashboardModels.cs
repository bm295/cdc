namespace DemoWeb.Models;

public sealed record CustomerRow(
    int Id,
    string FirstName,
    string LastName,
    string Email);

public sealed record ConnectorStatusDto(
    string Name,
    string State,
    string? WorkerId,
    IReadOnlyList<ConnectorTaskStatusDto> Tasks,
    string? Error);

public sealed record ConnectorTaskStatusDto(
    int Id,
    string State,
    string? WorkerId,
    string? Trace);

public sealed record TopicMessageDto(
    string Topic,
    int Partition,
    long Offset,
    string? Key,
    string? Operation,
    string? Database,
    string? Table,
    string Summary,
    string? Value,
    string? ErrorType,
    string? ErrorMessage,
    DateTimeOffset? KafkaTimestamp);

public sealed record DashboardSnapshot(
    DateTimeOffset RefreshedAt,
    ConnectorStatusDto Connector,
    IReadOnlyList<CustomerRow> SourceCustomers,
    IReadOnlyList<CustomerRow> ReplicaCustomers,
    IReadOnlyList<TopicMessageDto> CustomerTopicMessages,
    IReadOnlyList<TopicMessageDto> DeadLetterMessages,
    IReadOnlyList<string> Warnings);

public sealed record DemoActionRequest(
    int? CustomerId,
    string? FirstName,
    string? LastName,
    string? Email);

public sealed record DemoActionResponse(
    string Action,
    string Status,
    string Sql,
    string Message,
    int? CustomerId,
    DateTimeOffset StartedAt);

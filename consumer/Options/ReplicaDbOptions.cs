namespace CdcConsumer.Options;

public sealed class ReplicaDbOptions
{
    public const string SectionName = "ReplicaDb";

    public string ConnectionString { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("ReplicaDb:ConnectionString is required.");
        }
    }
}

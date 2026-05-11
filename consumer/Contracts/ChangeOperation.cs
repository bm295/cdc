namespace CdcConsumer;

public enum ChangeOperation
{
    Create,
    Update,
    Delete,
    Read,
    Tombstone,
    Truncate,
    Unknown
}

public static class ChangeOperationMapper
{
    public static ChangeOperation FromDebeziumCode(string? code)
    {
        return code switch
        {
            "c" => ChangeOperation.Create,
            "u" => ChangeOperation.Update,
            "d" => ChangeOperation.Delete,
            "r" => ChangeOperation.Read,
            "t" => ChangeOperation.Truncate,
            null or "" => ChangeOperation.Unknown,
            _ => ChangeOperation.Unknown
        };
    }
}

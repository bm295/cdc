using System.Text.Json.Serialization;

namespace CdcConsumer;

public sealed record CustomerRecord
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = "";

    [JsonPropertyName("last_name")]
    public string LastName { get; init; } = "";

    [JsonPropertyName("email")]
    public string Email { get; init; } = "";
}

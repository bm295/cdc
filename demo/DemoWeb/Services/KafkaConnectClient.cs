using System.Text.Json;
using DemoWeb.Models;
using DemoWeb.Options;
using Microsoft.Extensions.Options;

namespace DemoWeb.Services;

public sealed class KafkaConnectClient(HttpClient httpClient, IOptions<CdcDemoOptions> options)
{
    private readonly CdcDemoOptions _options = options.Value;

    public async Task<ConnectorStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var statusUrl =
            $"{_options.KafkaConnectBaseUrl.TrimEnd('/')}/connectors/{_options.ConnectorName}/status";

        try
        {
            using var response = await httpClient.GetAsync(statusUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ConnectorStatusDto(
                    _options.ConnectorName,
                    "unavailable",
                    null,
                    Array.Empty<ConnectorTaskStatusDto>(),
                    $"Kafka Connect returned HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var name = TryGetString(root, "name") ?? _options.ConnectorName;
            var connector = root.GetProperty("connector");
            var connectorState = TryGetString(connector, "state") ?? "unknown";
            var workerId = TryGetString(connector, "worker_id");
            var tasks = new List<ConnectorTaskStatusDto>();

            if (root.TryGetProperty("tasks", out var taskElements) && taskElements.ValueKind == JsonValueKind.Array)
            {
                foreach (var taskElement in taskElements.EnumerateArray())
                {
                    tasks.Add(new ConnectorTaskStatusDto(
                        TryGetInt32(taskElement, "id") ?? 0,
                        TryGetString(taskElement, "state") ?? "unknown",
                        TryGetString(taskElement, "worker_id"),
                        TryGetString(taskElement, "trace")));
                }
            }

            return new ConnectorStatusDto(name, connectorState, workerId, tasks, null);
        }
        catch (Exception ex)
        {
            return new ConnectorStatusDto(
                _options.ConnectorName,
                "unavailable",
                null,
                Array.Empty<ConnectorTaskStatusDto>(),
                ex.Message);
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind is JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;
    }
}

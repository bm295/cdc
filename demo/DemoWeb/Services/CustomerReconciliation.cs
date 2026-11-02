using DemoWeb.Models;

namespace DemoWeb.Services;

public static class CustomerReconciliation
{
    public static ReconciliationResult Compare(IReadOnlyList<CustomerRow> source, IReadOnlyList<CustomerRow> replica)
    {
        var sourceById = source.ToDictionary(row => row.Id);
        var replicaById = replica.ToDictionary(row => row.Id);
        var differences = new List<CustomerDifference>();
        var matching = 0;
        foreach (var id in sourceById.Keys.Union(replicaById.Keys).Order())
        {
            sourceById.TryGetValue(id, out var left);
            replicaById.TryGetValue(id, out var right);
            if (left == right) { matching++; continue; }
            differences.Add(new CustomerDifference(id,
                left is null ? "extra_in_replica" : right is null ? "missing_in_replica" : "different",
                left, right));
        }
        return new ReconciliationResult(DateTimeOffset.UtcNow, source.Count, replica.Count, matching, differences);
    }
}

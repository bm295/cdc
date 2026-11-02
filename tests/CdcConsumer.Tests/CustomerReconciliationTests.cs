using DemoWeb.Models;
using DemoWeb.Services;
using Xunit;

namespace CdcConsumer.Tests;

public class CustomerReconciliationTests
{
    [Fact]
    public void ReportsMatchingMissingExtraAndChangedRowsInIdOrder()
    {
        var same = new CustomerRow(1, "A", "B", "a@example.test");
        var source = new[] { same, new CustomerRow(2, "C", "D", "c@example.test"), new CustomerRow(4, "E", "F", "old@example.test") };
        var replica = new[] { same, new CustomerRow(3, "G", "H", "g@example.test"), new CustomerRow(4, "E", "F", "new@example.test") };

        var result = CustomerReconciliation.Compare(source, replica);

        Assert.Equal(1, result.MatchingCount);
        Assert.Equal(new[] { 2, 3, 4 }, result.Differences.Select(d => d.Id));
        Assert.Equal(new[] { "missing_in_replica", "extra_in_replica", "different" }, result.Differences.Select(d => d.Kind));
    }

    [Fact]
    public void EmptyTablesMatch()
    {
        var result = CustomerReconciliation.Compare(Array.Empty<CustomerRow>(), Array.Empty<CustomerRow>());
        Assert.Empty(result.Differences);
        Assert.Equal(0, result.MatchingCount);
    }
}

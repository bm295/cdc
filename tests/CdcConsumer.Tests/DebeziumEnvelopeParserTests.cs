using CdcConsumer;
using CdcConsumer.Application;
using Xunit;

namespace CdcConsumer.Tests;

public sealed class DebeziumEnvelopeParserTests
{
    private readonly DebeziumEnvelopeParser _parser = new();

    [Fact]
    public void Parse_CreateEvent_ReturnsCreateChange()
    {
        var change = _parser.Parse<CustomerRecord>(
            Message("""
            {
              "before": null,
              "after": {
                "id": 1,
                "first_name": "Anne",
                "last_name": "Kretchmar",
                "email": "annek@noanswer.org"
              },
              "source": {
                "db": "inventory",
                "table": "customers",
                "ts_ms": 1710000000000
              },
              "op": "c",
              "ts_ms": 1710000001000
            }
            """));

        Assert.Equal(ChangeOperation.Create, change.Operation);
        Assert.Null(change.Before);
        Assert.NotNull(change.After);
        Assert.Equal(1, change.After.Id);
        Assert.Equal("customers", change.Source?.Table);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1710000001000), change.OccurredAt);
    }

    [Fact]
    public void Parse_UpdateEvent_ReturnsBeforeAndAfter()
    {
        var change = _parser.Parse<CustomerRecord>(
            Message("""
            {
              "before": {
                "id": 1,
                "first_name": "Anne",
                "last_name": "Kretchmar",
                "email": "old@example.com"
              },
              "after": {
                "id": 1,
                "first_name": "Anne",
                "last_name": "Kretchmar",
                "email": "new@example.com"
              },
              "source": {
                "db": "inventory",
                "table": "customers"
              },
              "op": "u",
              "ts_ms": 1710000001000
            }
            """));

        Assert.Equal(ChangeOperation.Update, change.Operation);
        Assert.Equal("old@example.com", change.Before?.Email);
        Assert.Equal("new@example.com", change.After?.Email);
    }

    [Fact]
    public void Parse_DeleteEvent_ReturnsBeforePayload()
    {
        var change = _parser.Parse<CustomerRecord>(
            Message("""
            {
              "before": {
                "id": 1,
                "first_name": "Anne",
                "last_name": "Kretchmar",
                "email": "annek@noanswer.org"
              },
              "after": null,
              "source": {
                "db": "inventory",
                "table": "customers"
              },
              "op": "d",
              "ts_ms": 1710000001000
            }
            """));

        Assert.Equal(ChangeOperation.Delete, change.Operation);
        Assert.NotNull(change.Before);
        Assert.Null(change.After);
    }

    [Fact]
    public void Parse_Tombstone_ReturnsTombstoneChange()
    {
        var change = _parser.Parse<CustomerRecord>(
            new ConsumedMessage(
                "mysql-server-1.inventory.customers",
                0,
                42,
                """{"id":1}""",
                null));

        Assert.Equal(ChangeOperation.Tombstone, change.Operation);
        Assert.Null(change.Before);
        Assert.Null(change.After);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            _parser.Parse<CustomerRecord>(Message("{not-json}")));
    }

    private static ConsumedMessage Message(string value)
    {
        return new ConsumedMessage(
            "mysql-server-1.inventory.customers",
            0,
            10,
            """{"id":1}""",
            value);
    }
}

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
    public void Parse_KafkaConnectJsonEnvelope_ReturnsPayloadChange()
    {
        var change = _parser.Parse<CustomerRecord>(
            Message("""
            {
              "schema": {
                "type": "struct"
              },
              "payload": {
                "before": null,
                "after": {
                  "id": 2,
                  "first_name": "Maggie",
                  "last_name": "Smith",
                  "email": "maggie@example.com"
                },
                "source": {
                  "db": "inventory",
                  "table": "customers",
                  "ts_ms": 1710000000000
                },
                "op": "r",
                "ts_ms": 1710000001000
              }
            }
            """));

        Assert.Equal(ChangeOperation.Read, change.Operation);
        Assert.Equal(2, change.After?.Id);
        Assert.Equal("inventory", change.Source?.Database);
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
    public void Parse_TruncateEvent_ReturnsTruncateChange()
    {
        var change = _parser.Parse<CustomerRecord>(
            Message("""
            {
              "before": null,
              "after": null,
              "source": {
                "db": "inventory",
                "table": "customers"
              },
              "op": "t",
              "ts_ms": 1710000001000
            }
            """));

        Assert.Equal(ChangeOperation.Truncate, change.Operation);
        Assert.Null(change.Before);
        Assert.Null(change.After);
        Assert.Equal("customers", change.Source?.Table);
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

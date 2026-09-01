using CdcConsumer.Application.Customers;
using CdcConsumer.Infrastructure.ReplicaDb;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CdcConsumer.Tests;

public sealed class CustomerChangeHandlerTests
{
    [Fact]
    public async Task HandleAsync_TruncateEvent_TruncatesReplicaStore()
    {
        var store = new RecordingReplicaCustomerStore();
        var handler = new CustomerChangeHandler(NullLogger<CustomerChangeHandler>.Instance, store);

        await handler.HandleAsync(
            new ChangeEvent<CustomerRecord>(
                new ChangeMetadata("mysql-server-1.inventory.customers", 0, 10, null),
                ChangeOperation.Truncate,
                null,
                null,
                new DebeziumSource { Database = "inventory", Table = "customers" },
                DateTimeOffset.FromUnixTimeMilliseconds(1710000001000)),
            CancellationToken.None);

        Assert.Equal(1, store.TruncateCalls);
        Assert.Equal(0, store.UpsertCalls);
        Assert.Equal(0, store.DeleteCalls);
    }

    private sealed class RecordingReplicaCustomerStore : IReplicaCustomerStore
    {
        public int DeleteCalls { get; private set; }
        public int TruncateCalls { get; private set; }
        public int UpsertCalls { get; private set; }

        public Task DeleteAsync(int id, CancellationToken cancellationToken)
        {
            DeleteCalls++;
            return Task.CompletedTask;
        }

        public Task TruncateAsync(CancellationToken cancellationToken)
        {
            TruncateCalls++;
            return Task.CompletedTask;
        }

        public Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken)
        {
            UpsertCalls++;
            return Task.CompletedTask;
        }
    }
}

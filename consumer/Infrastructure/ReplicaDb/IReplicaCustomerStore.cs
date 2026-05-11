namespace CdcConsumer.Infrastructure.ReplicaDb;

public interface IReplicaCustomerStore
{
    Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task TruncateAsync(CancellationToken cancellationToken);
}

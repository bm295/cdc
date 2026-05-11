using CdcConsumer.Infrastructure.ReplicaDb;
using Microsoft.Extensions.Logging;

namespace CdcConsumer.Application.Customers;

public sealed class CustomerChangeHandler(ILogger<CustomerChangeHandler> logger, IReplicaCustomerStore replicaStore) : IChangeHandler<CustomerRecord>
{
    public Task HandleAsync(ChangeEvent<CustomerRecord> changeEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (changeEvent.Operation)
        {
            case ChangeOperation.Create:
            case ChangeOperation.Read:
                var createdOrRead = Require(changeEvent.After, changeEvent.Operation, "after");
                await replicaStore.UpsertAsync(createdOrRead, cancellationToken);
                LogUpsert(changeEvent, createdOrRead);
                break;

            case ChangeOperation.Update:
                var updated = Require(changeEvent.After, changeEvent.Operation, "after");
                await replicaStore.UpsertAsync(updated, cancellationToken);
                LogUpdate(changeEvent, updated);
                break;

            case ChangeOperation.Delete:
                var deleted = Require(changeEvent.Before, changeEvent.Operation, "before");
                await replicaStore.DeleteAsync(deleted.Id, cancellationToken);
                LogDelete(changeEvent, deleted);
                break;

            case ChangeOperation.Truncate:
                await replicaStore.TruncateAsync(cancellationToken);

                logger.LogWarning(
                    "Received truncate event for {Database}.{Table} at {Topic}[{Partition}]@{Offset}.",
                    changeEvent.Source?.Database,
                    changeEvent.Source?.Table,
                    changeEvent.Metadata.Topic,
                    changeEvent.Metadata.Partition,
                    changeEvent.Metadata.Offset);
                break;

            default:
                throw new InvalidDataException($"Unsupported customer change operation '{changeEvent.Operation}'.");
        }

        return Task.CompletedTask;
    }

    private void LogUpsert(ChangeEvent<CustomerRecord> changeEvent, CustomerRecord customer)
    {
        logger.LogInformation(
            "Customer {Operation}: {CustomerId} {FirstName} {LastName} {Email} from {Topic}[{Partition}]@{Offset}.",
            changeEvent.Operation,
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            changeEvent.Metadata.Topic,
            changeEvent.Metadata.Partition,
            changeEvent.Metadata.Offset);
    }

    private void LogUpdate(ChangeEvent<CustomerRecord> changeEvent, CustomerRecord customer)
    {
        logger.LogInformation(
            "Customer update: {CustomerId} {Email} from {Topic}[{Partition}]@{Offset}.",
            customer.Id,
            customer.Email,
            changeEvent.Metadata.Topic,
            changeEvent.Metadata.Partition,
            changeEvent.Metadata.Offset);
    }

    private void LogDelete(ChangeEvent<CustomerRecord> changeEvent, CustomerRecord customer)
    {
        logger.LogInformation(
            "Customer delete: {CustomerId} {Email} from {Topic}[{Partition}]@{Offset}.",
            customer.Id,
            customer.Email,
            changeEvent.Metadata.Topic,
            changeEvent.Metadata.Partition,
            changeEvent.Metadata.Offset);
    }

    private static T Require<T>(T? value, ChangeOperation operation, string field)
    {
        return value ?? throw new InvalidDataException(
            $"Customer {operation} event is missing required Debezium '{field}' payload.");
    }
}

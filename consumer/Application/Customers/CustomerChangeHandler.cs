using Microsoft.Extensions.Logging;

namespace CdcConsumer.Application.Customers;

public sealed class CustomerChangeHandler(ILogger<CustomerChangeHandler> logger) : IChangeHandler<CustomerRecord>
{
    public Task HandleAsync(ChangeEvent<CustomerRecord> changeEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (changeEvent.Operation)
        {
            case ChangeOperation.Create:
            case ChangeOperation.Read:
                LogUpsert(changeEvent, Require(changeEvent.After, changeEvent.Operation, "after"));
                break;

            case ChangeOperation.Update:
                LogUpdate(changeEvent, Require(changeEvent.After, changeEvent.Operation, "after"));
                break;

            case ChangeOperation.Delete:
                LogDelete(changeEvent, Require(changeEvent.Before, changeEvent.Operation, "before"));
                break;

            case ChangeOperation.Truncate:
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

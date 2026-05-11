namespace CdcConsumer.Application;

public interface IChangeHandler<T>
{
    Task HandleAsync(ChangeEvent<T> changeEvent, CancellationToken cancellationToken);
}

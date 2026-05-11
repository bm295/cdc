namespace CdcConsumer.Application;

public interface IDebeziumEnvelopeParser
{
    ChangeEvent<T> Parse<T>(ConsumedMessage message);
}

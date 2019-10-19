namespace Ntrada.Extensions.RabbitMq
{
    public interface ISpanContextBuilder
    {
        string Build(ExecutionData executionData);
    }
}
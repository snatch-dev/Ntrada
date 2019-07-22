namespace Ntrada.Handlers.RabbitMq
{
    public interface IContextBuilder
    {
        object Build(ExecutionData executionData);
    }
}
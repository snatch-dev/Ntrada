namespace Ntrada.Handlers.RabbitMq
{
    internal sealed class NullContextBuilder : IContextBuilder
    {
        public object Build(ExecutionData executionData) => null;
    }
}
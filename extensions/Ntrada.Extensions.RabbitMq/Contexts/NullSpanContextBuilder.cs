namespace Ntrada.Extensions.RabbitMq.Contexts
{
    internal sealed class NullSpanContextBuilder : ISpanContextBuilder
    {
        public string Build(ExecutionData executionData) => null;
    }
}
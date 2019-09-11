using Ntrada.Core;

namespace Ntrada.Extensions.RabbitMq
{
    internal sealed class NullContextBuilder : IContextBuilder
    {
        public object Build(ExecutionData executionData) => null;
    }
}
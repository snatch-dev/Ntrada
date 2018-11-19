using System.Threading.Tasks;

namespace NGate.Framework
{
    public interface IExtension
    {
        string Name { get; }
        Task InitAsync();
        Task ExecuteAsync(ExecutionData executionData);
        Task CloseAsync();
    }
}
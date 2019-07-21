using System.Threading.Tasks;

namespace Ntrada
{
    public interface IExtension
    {
        string Name { get; }
        Task InitAsync();
        Task ExecuteAsync(ExecutionData executionData);
        Task CloseAsync();
    }
}
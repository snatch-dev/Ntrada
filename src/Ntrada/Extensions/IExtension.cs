using System.Threading.Tasks;
using Ntrada.Models;

namespace Ntrada.Extensions
{
    public interface IExtension
    {
        string Name { get; }
        Task InitAsync();
        Task ExecuteAsync(ExecutionData executionData);
        Task CloseAsync();
    }
}
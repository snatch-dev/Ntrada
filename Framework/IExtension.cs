using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NGate.Framework
{
    public interface IExtension
    {
        string Name { get; }
        Task InitAsync(Configuration configuration);
        Task ExecuteAsync(ExecutionData executionData);
        Task CloseAsync();
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ntrada.Samples.Services.Orders.Models;
using Ntrada.Samples.Services.Orders.Requests;

namespace Ntrada.Samples.Services.Orders.Services
{
    public interface IOrdersService
    {
        Task<IEnumerable<OrderDto>> BrowseAsync();
        Task<OrderDto> GetAsync(Guid id);
        Task CreateAsync(CreateOrder request);
        Task DeleteAsync(Guid id);
    }
}
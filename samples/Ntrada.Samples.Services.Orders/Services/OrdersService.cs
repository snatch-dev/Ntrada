using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ntrada.Samples.Services.Orders.Models;
using Ntrada.Samples.Services.Orders.Requests;

namespace Ntrada.Samples.Services.Orders.Services
{
    public class OrdersService : IOrdersService
    {
        private static readonly ConcurrentDictionary<Guid, OrderDto> Orders =
            new ConcurrentDictionary<Guid, OrderDto>
            {
                [GetGuid(1)] = new OrderDto
                {
                    Id = GetGuid(1),
                    Price = 100
                },
                [GetGuid(2)] = new OrderDto
                {
                    Id = GetGuid(2),
                    Price = 200
                },
                [GetGuid(3)] = new OrderDto
                {
                    Id = GetGuid(3),
                    Price = 300
                }
            };

        public Task<IEnumerable<OrderDto>> BrowseAsync()
        {
            var orders = Orders.Values;

            return Task.FromResult(orders.AsEnumerable());
        }

        public Task<OrderDto> GetAsync(Guid id)
        {
            Orders.TryGetValue(id, out var order);

            return Task.FromResult(order);
        }

        public Task CreateAsync(CreateOrder request)
        {
            var added = Orders.TryAdd(request.Id, new OrderDto
            {
                Id = request.Id,
                Price = request.Price
            });

            if (!added)
            {
                throw new ArgumentException($"Order with id: '{request.Id}' already exists.");
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            Orders.TryRemove(id, out _);
            
            return Task.CompletedTask;
        }

        private static Guid GetGuid(int digit) => Guid.Parse($"{digit}0000000-0000-0000-0000-000000000000");
    }
}
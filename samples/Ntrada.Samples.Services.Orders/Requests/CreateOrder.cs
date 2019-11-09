using System;

namespace Ntrada.Samples.Services.Orders.Requests
{
    public class CreateOrder
    {
        public Guid Id { get; }
        public decimal Price { get; }

        public CreateOrder(Guid id, decimal price)
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id;
            Price = price;
        }
    }
}
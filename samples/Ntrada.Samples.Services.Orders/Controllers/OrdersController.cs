using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Ntrada.Samples.Services.Orders.Models;
using Ntrada.Samples.Services.Orders.Requests;
using Ntrada.Samples.Services.Orders.Services;

namespace Ntrada.Samples.Services.Orders.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrdersService _ordersService;

        public OrdersController(IOrdersService ordersService)
        {
            _ordersService = ordersService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderDto>>> Get()
        {
            var orders = await _ordersService.BrowseAsync();

            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> Get(Guid id, string meta)
        {
            var order = await _ordersService.GetAsync(id);
            if (order is null)
            {
                return NotFound();
            }

            order.Meta = meta;

            return order;
        }
        
        [HttpPost]
        public async Task<ActionResult> Post(CreateOrder request)
        {
            try
            {
                await _ordersService.CreateAsync(request);
                return CreatedAtAction(nameof(Get), new {id = request.Id}, null);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            await _ordersService.DeleteAsync(id);
            return NoContent();
        }
    }
}
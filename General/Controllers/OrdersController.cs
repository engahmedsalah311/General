using System;
using System.Threading.Tasks;
using Application.DTOs.Orders;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace General.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        /// <summary>
        /// Get all orders for the current user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isAdmin = false;
                if(userRole == "Admin") isAdmin = true;
                var orders = await _orderService.GetOrdersAsync(new OrderFilterDto(),userId,isAdmin);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, "An error occurred while getting orders");
            }
        }

        /// <summary>
        /// Get a specific order by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isAdmin = false;
                if (userRole == "Admin") isAdmin = true;
                var order = await _orderService.GetOrderByIdAsync(id, userId,isAdmin);
                return Ok(order);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Order not found");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting order with ID {id}");
                return StatusCode(500, "An error occurred while getting the order");
            }
        }

        /// <summary>
        /// Create a new order from the current user's cart
        /// </summary>
        
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto createOrderDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var order = await _orderService.CreateOrderAsync(createOrderDto, userId);
                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, "An error occurred while creating the order");
            }
        }

        /// <summary>
        /// Update order status (Admin only)
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto updateStatusDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = false;
                if (userRole == "Admin") isAdmin = true;
                await _orderService.UpdateOrderStatusAsync(id, updateStatusDto,userId,isAdmin);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Order not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for order {id}");
                return StatusCode(500, "An error occurred while updating the order status");
            }
        }

        /// <summary>
        /// Cancel an order
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            try
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = false;
                if (userRole == "Admin") isAdmin = true;
                await _orderService.CancelOrderAsync(id, userId,isAdmin);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Order not found");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling order {id}");
                return StatusCode(500, "An error occurred while cancelling the order");
            }
        }

        /// <summary>
        /// Get all orders (Admin only)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders([FromQuery] OrderFilterDto filter)
        {
            try
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = false;
                if (userRole == "Admin") isAdmin = true;
                var orders = await _orderService.GetOrdersAsync(filter,userId,isAdmin);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all orders");
                return StatusCode(500, "An error occurred while getting orders");
            }
        }
    }
}

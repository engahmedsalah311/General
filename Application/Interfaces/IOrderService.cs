using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Orders;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IOrderService
    {
        Task<OrderListDto> GetOrdersAsync(OrderFilterDto filter, string userId, bool isAdmin);
        Task<OrderDto> GetOrderByIdAsync(int id, string userId, bool isAdmin);
        Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto, string userId);
        Task<OrderDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto updateDto, string userId, bool isAdmin);
        Task<bool> CancelOrderAsync(int id, string userId, bool isAdmin);
        Task<bool> ProcessPaymentAsync(int orderId, string userId);
        Task<bool> RefundOrderAsync(int orderId, string userId, bool isAdmin);
        Task<OrderDto> AddOrderItemAsync(int orderId, AddOrderItemDto itemDto, string userId);
        Task<bool> RemoveOrderItemAsync(int orderId, int itemId, string userId);
    }
}

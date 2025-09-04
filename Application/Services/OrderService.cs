using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Security.Claims;
using Application.DTOs.Orders;
using Application.DTOs.Payments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrderService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentService _paymentService;

        public OrderService(
            IUnitOfWork unitOfWork, 
            ILogger<OrderService> logger,
            UserManager<ApplicationUser> userManager,
            IPaymentService paymentService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userManager = userManager;
            _paymentService = paymentService;
        }

        public async Task<OrderListDto> GetOrdersAsync(OrderFilterDto filter, string userId, bool isAdmin)
        {
            try
            {
                // Build filters
                var filters = new List<Expression<Func<Order, bool>>>();
                
                // Non-admin users can only see their own orders
                if (!isAdmin)
                {
                    filters.Add(o => o.UserId == userId);
                }

                // Apply filters
                if (filter.Id.HasValue)
                {
                    filters.Add(o => o.Id == filter.Id.Value);
                }

                if (!string.IsNullOrWhiteSpace(filter.OrderNumber))
                {
                    filters.Add(o => o.OrderNumber.Contains(filter.OrderNumber));
                }

                if (!string.IsNullOrWhiteSpace(filter.UserId))
                {
                    filters.Add(o => o.UserId == filter.UserId);
                }

                if (filter.Status.HasValue)
                {
                    // Explicitly use Domain.Enums.OrderStatus
                    filters.Add(o => o.Status == (Domain.Enums.OrderStatus)filter.Status.Value);
                }

                if (filter.FromDate.HasValue)
                {
                    filters.Add(o => o.CreatedAt >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    var toDate = filter.ToDate.Value.Date.AddDays(1);
                    filters.Add(o => o.CreatedAt < toDate);
                }

                if (filter.MinTotal.HasValue)
                {
                    filters.Add(o => o.TotalAmount >= filter.MinTotal.Value);
                }

                if (filter.MaxTotal.HasValue)
                {
                    filters.Add(o => o.TotalAmount <= filter.MaxTotal.Value);
                }

                // Define includes
                var includes = new List<Expression<Func<Order, object>>>
                {
                    o => o.OrderItems,
                    o => o.OrderItems.Select(oi => oi.Product),
                    o => o.User
                };

                // Define order by
                Func<IQueryable<Order>, IOrderedQueryable<Order>> orderBy = filter.SortBy.ToLower() switch
                {
                    "ordernumber" => filter.SortDescending 
                        ? q => q.OrderByDescending(o => o.OrderNumber)
                        : q => q.OrderBy(o => o.OrderNumber),
                    "ordertotal" => filter.SortDescending
                        ? q => q.OrderByDescending(o => o.TotalAmount)
                        : q => q.OrderBy(o => o.TotalAmount),
                    "orderdate" => filter.SortDescending
                        ? q => q.OrderByDescending(o => o.CreatedAt)
                        : q => q.OrderBy(o => o.CreatedAt),
                    _ => filter.SortDescending
                        ? q => q.OrderByDescending(o => o.Id)
                        : q => q.OrderBy(o => o.Id)
                };

                // Get paginated results using repository
                var pagedResult = await _unitOfWork.Repository<Order>()
                    .GetPagedMappedAsync<OrderDto>(
                        pageNumber: filter.PageNumber,
                        pageSize: filter.PageSize,
                        filters: filters,
                        orderBy: orderBy,
                        includes: includes
                    );

                return new OrderListDto
                {
                    Items = pagedResult.Items,
                    TotalCount = pagedResult.TotalCount,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                throw;
            }
        }

        public async Task<OrderDto> GetOrderByIdAsync(int id, string userId, bool isAdmin)
        {
            try
            {
                // Build filters
                var filters = new List<Expression<Func<Order, bool>>>
                {
                    o => o.Id == id
                };

                // If not admin, add user filter
                if (!isAdmin)
                {
                    filters.Add(o => o.UserId == userId);
                }

                // Define includes
                var includes = new List<Expression<Func<Order, object>>>
                {
                    o => o.OrderItems,
                    o => o.OrderItems.Select(oi => oi.Product),
                    o => o.User
                };

                // Get order with includes
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultIncAsync(filters, includes);

                if (order == null)
                {
                    throw new KeyNotFoundException($"Order with ID {id} not found");
                }

                return new OrderDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    UserId = order.UserId,
                    CustomerEmail = order.User?.Email,
                    OrderTotal = order.TotalAmount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = oi.Product?.Name,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        //TotalPrice = oi.TotalPrice
                    }).ToList()
                };
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order with ID {OrderId}", id);
                throw;
            }
        }

        private async Task ReturnOrderItemsToStockAsync(Order order)
        {
            var productRepo = _unitOfWork.Repository<Product>();
            
            foreach (var item in order.OrderItems)
            {
                var product = await productRepo.GetByIdAsync<object>(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    productRepo.Update(product);
                }
            }
            
            await _unitOfWork.CompleteAsync();
        }

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto, string userId)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                // Get user
                var user = await _userManager.FindByIdAsync(userId) ?? 
                    throw new ArgumentException("User not found");

                // Create order
                var order = new Order
                {
                    UserId = userId,
                    OrderNumber = GenerateOrderNumber(),
                    Status = Domain.Entities.OrderStatus.Pending,
                    TotalAmount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Add order items and calculate total
                decimal totalAmount = 0;
                var orderItems = new List<OrderItem>();
                
                // Get product repository
                var productRepo = _unitOfWork.Repository<Product>();
                
                // Process each order item
                foreach (var item in createOrderDto.OrderItems)
                {
                    // Get product with filters
                    var product = await productRepo.FirstOrDefaultAsync(
                        predicate: p => p.Id == item.ProductId && p.IsActive
                    );

                    if (product == null)
                    {
                        throw new KeyNotFoundException($"Product with ID {item.ProductId} not found or inactive");
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient stock for product {product.Name}");
                    }

                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price,
                        //TotalPrice = product.Price * item.Quantity
                    };

                    totalAmount += orderItem.TotalPrice;
                    orderItems.Add(orderItem);

                    // Update product stock
                    product.StockQuantity -= item.Quantity;
                    productRepo.Update(product);
                }

                // Update order total and items
                order.TotalAmount = totalAmount;
                order.OrderItems = orderItems;

                // Save order
                await _unitOfWork.Repository<Order>().AddAsync(order);
                await _unitOfWork.CompleteAsync();

                // Process payment
                var paymentResult = await _paymentService.ProcessPaymentAsync(new ProcessPaymentDto
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    PaymentMethod = createOrderDto.PaymentMethod,
                    PaymentDetails = createOrderDto.PaymentDetails
                });

                if (!paymentResult.Success)
                {
                    await _unitOfWork.Rollback();
                    throw new InvalidOperationException($"Payment failed: {paymentResult.Message}");
                }

                // Update order status
                order.Status = Domain.Enums.OrderStatus.Processing;
                order.PaymentStatus = Domain.Enums.PaymentStatus.Paid;
                order.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.CompleteAsync();

                await _unitOfWork.CommitAsync();

                return await GetOrderByIdAsync(order.Id, userId, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for user {UserId}", userId);
                await _unitOfWork.Rollback();
                throw;
            }
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto updateDto, string userId, bool isAdmin)
        {
            try
            {
                // Build filters
                var filters = new List<Expression<Func<Order, bool>>>
                {
                    o => o.Id == id
                };

                // If not admin, add user filter
                if (!isAdmin)
                {
                    filters.Add(o => o.UserId == userId);
                }

                // Get order with includes
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultAsync(
                        predicate: filters.Count == 1 ? filters[0] : filters.Aggregate((current, next) => {
                            var param = Expression.Parameter(typeof(Order), "o");
                            var body = Expression.AndAlso(
                                Expression.Invoke(current, param),
                                Expression.Invoke(next, param)
                            );
                            return Expression.Lambda<Func<Order, bool>>(body, param);
                        }),
                        include: source => {
                            var query = source;
                            query = query.Include(o => o.OrderItems)
                                       .ThenInclude(oi => oi.Product);
                            query = query.Include(o => o.User);
                            return query;
                        }
                    );

                if (order == null)
                {
                    throw new KeyNotFoundException($"Order with ID {id} not found");
                }

                // Validate status transition
                if (!IsValidStatusTransition(order.Status, updateDto.Status))
                {
                    throw new InvalidOperationException($"Invalid status transition from {order.Status} to {updateDto.Status}");
                }

                // Update order status
                order.Status = updateDto.Status;
                order.UpdatedAt = DateTime.UtcNow;

                // If order is being cancelled, return stock
                if (updateDto.Status == DTOs.Orders.OrderStatus.Cancelled)
                {
                    await ReturnOrderItemsToStockAsync(order);
                }

                // Update order
                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.CompleteAsync();

                return await GetOrderByIdAsync(order.Id, userId, isAdmin);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId} to {Status}", id, updateDto.Status);
                throw;
            }
        }

        public async Task<bool> CancelOrderAsync(int id, string userId, bool isAdmin)
        {
            try
            {
                // Build filters
                var filters = new List<Expression<Func<Order, bool>>>
                {
                    o => o.Id == id
                };

                // If not admin, add user filter
                if (!isAdmin)
                {
                    filters.Add(o => o.UserId == userId);
                }

                // Get order with includes
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultIncAsync(
                        filters: filters,
                        includes: new List<Expression<Func<Order, object>>>
                        {
                            o => o.OrderItems,
                            o => o.OrderItems.Select(oi => oi.Product)
                        }
                    );

                if (order == null)
                {
                    throw new KeyNotFoundException($"Order with ID {id} not found");
                }

                // Check if order can be cancelled
                if (order.Status != Domain.Enums.OrderStatus.Pending && order.Status != Domain.Enums.OrderStatus.Processing)
                {
                    throw new InvalidOperationException($"Cannot cancel order with status {order.Status}");
                }

                // Update order status
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                // Return items to stock
                await ReturnOrderItemsToStockAsync(order);

                // Update order
                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.SaveAsync();

                return true;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", id);
                throw;
            }
        }

        public async Task<bool> ProcessPaymentAsync(int orderId, string userId)
        {
            try
            {
                // Build filters
                var filters = new List<Expression<Func<Order, bool>>>
                {
                    o => o.Id == orderId,
                    o => o.UserId == userId,
                    o => o.Status == OrderStatus.Pending
                };

                // Get order with includes
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultIncAsync(
                        filters: filters,
                        includes: new List<Expression<Func<Order, object>>>
                        {
                            o => o.User
                        }
                    );

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found or not in pending status for user {UserId}", orderId, userId);
                    return false;
                }

                // Process payment
                var paymentResult = await _paymentService.ProcessPaymentAsync(new ProcessPaymentDto
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    PaymentMethod = order.PaymentMethod,
                    PaymentDetails = order.PaymentDetails
                });

                if (paymentResult.Success)
                {
                    // Update order status and payment details
                    order.Status = OrderStatus.Processing;
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.TransactionId = paymentResult.TransactionId;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Update order
                    _unitOfWork.Repository<Order>().Update(order);
                    await _unitOfWork.SaveAsync();

                    _logger.LogInformation("Payment processed successfully for order {OrderId}", orderId);
                    return true;
                }

                _logger.LogWarning("Payment failed for order {OrderId}", orderId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for order {OrderId}", orderId);
                throw;
            }
        }

        public async Task<bool> RefundOrderAsync(int orderId, string userId, bool isAdmin)
        {
            try
            {
                // Build filters
                var filters = new List<Expression<Func<Order, bool>>>
                {
                    o => o.Id == orderId
                };

                // If not admin, add user filter
                if (!isAdmin)
                {
                    filters.Add(o => o.UserId == userId);
                }

                // Get order with includes
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultIncAsync(
                        filters: filters,
                        includes: new List<Expression<Func<Order, object>>>
                        {
                            o => o.OrderItems,
                            o => o.OrderItems.Select(oi => oi.Product)
                        }
                    );

                if (order == null)
                {
                    throw new KeyNotFoundException($"Order with ID {orderId} not found");
                }

                // Check if order can be refunded
                if (order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Processing)
                {
                    throw new InvalidOperationException($"Cannot refund order with status {order.Status}");
                }

                if (order.PaymentStatus != PaymentStatus.Paid)
                {
                    throw new InvalidOperationException("Cannot refund order that hasn't been paid");
                }

                // Process refund
                var refundResult = await _paymentService.ProcessRefundAsync(new ProcessRefundDto
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    TransactionId = order.TransactionId
                });

                if (!refundResult.Success)
                {
                    _logger.LogWarning("Refund failed for order {OrderId}", orderId);
                    return false;
                }

                // Update order status
                order.Status = OrderStatus.Refunded;
                order.PaymentStatus = PaymentStatus.Refunded;
                order.UpdatedAt = DateTime.UtcNow;

                // Return items to stock
                await ReturnOrderItemsToStockAsync(order);

                // Update order
                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Order {OrderId} refunded successfully", orderId);
                return true;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding order {OrderId}", orderId);
                throw;
            }
        }

        public async Task<OrderDto> AddOrderItemAsync(int orderId, AddOrderItemDto itemDto, string userId)
        {
            var orderRepo = _unitOfWork.Repository<Order>();
            var order = await orderRepo.GetAll()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null || order.Status != OrderStatus.Pending)
                return null;

            var productRepo = _unitOfWork.Repository<Product>();
            var product = await productRepo.GetByIdAsync(itemDto.ProductId);

            if (product == null || product.StockQuantity < itemDto.Quantity)
                return null;

            // Add order item
            var orderItem = new OrderItem
            {
                OrderId = orderId,
                ProductId = itemDto.ProductId,
                Quantity = itemDto.Quantity,
                UnitPrice = product.Price,
                TotalPrice = product.Price * itemDto.Quantity
            };

            // Update order total
            order.TotalAmount += orderItem.TotalPrice;
            order.UpdatedAt = DateTime.UtcNow;

            // Update product stock
            product.StockQuantity -= itemDto.Quantity;

            // Save changes
            await _unitOfWork.Repository<OrderItem>().AddAsync(orderItem);
            await productRepo.UpdateAsync(product);
            await orderRepo.UpdateAsync(order);
            await _unitOfWork.CommitAsync();

            // Return updated order
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                UserId = order.UserId,
                CustomerEmail = order.User?.Email,
                OrderTotal = order.TotalAmount,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                OrderItems = order.OrderItems?.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product?.Name,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice
                }).ToList()
            };
        }

        public async Task<bool> RemoveOrderItemAsync(int orderId, int itemId, string userId)
        {
            var orderRepo = _unitOfWork.Repository<Order>();
            var order = await orderRepo.GetAll()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null || order.Status != OrderStatus.Pending)
                return false;

            var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == itemId);
            if (orderItem == null)
                return false;

            // Update order total
            order.TotalAmount -= orderItem.TotalPrice;
            order.UpdatedAt = DateTime.UtcNow;

            // Update product stock
            var productRepo = _unitOfWork.Repository<Product>();
            var product = await productRepo.GetByIdAsync(orderItem.ProductId);
            if (product != null)
            {
                product.StockQuantity += orderItem.Quantity;
                await productRepo.UpdateAsync(product);
            }

            // Remove order item
            var orderItemRepo = _unitOfWork.Repository<OrderItem>();
            await orderItemRepo.DeleteAsync(orderItem);
            await orderRepo.UpdateAsync(order);
            await _unitOfWork.CommitAsync();

            return true;
        }

        private string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }

        private bool IsValidStatusTransition(Domain.Enums.OrderStatus currentStatus, Domain.Enums.OrderStatus newStatus)
        {
            return newStatus switch
            {
                Domain.Enums.OrderStatus.Pending => false, // Can't go back to pending
                Domain.Enums.OrderStatus.Processing => currentStatus == Domain.Enums.OrderStatus.Pending,
                Domain.Enums.OrderStatus.Shipped => currentStatus == Domain.Enums.OrderStatus.Processing,
                Domain.Enums.OrderStatus.Delivered => currentStatus == Domain.Enums.OrderStatus.Shipped,
                Domain.Enums.OrderStatus.Cancelled => currentStatus == Domain.Enums.OrderStatus.Pending || currentStatus == Domain.Enums.OrderStatus.Processing,
                _ => false
            };
        }

        private async Task RestoreOrderItemsStock(int orderId)
        {
            var orderItemRepo = _unitOfWork.Repository<OrderItem>();
            var productRepo = _unitOfWork.Repository<Product>();

            var orderItems = await orderItemRepo.GetAll()
                .Where(oi => oi.OrderId == orderId)
                .Include(oi => oi.Product)
                .ToListAsync();

            foreach (var item in orderItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockQuantity += item.Quantity;
                    await productRepo.UpdateAsync(item.Product);
                }
            }
        }
    }
}

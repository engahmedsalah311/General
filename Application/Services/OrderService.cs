using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.Orders;
using Application.DTOs.Payments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainEnums = Domain.Enums;

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
                    filters.Add(o => o.Status.ToString() == filter.Status.Value.ToString());
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
                var (data, totalCount) = await _unitOfWork.Repository<Order>()
                    .GetPagedMappedAsync<OrderDto>(
                        page: filter.PageNumber,
                        pageSize: filter.PageSize,
                        filters: filters,
                        includes: includes
                    );

                return new OrderListDto
                {
                    Items = data,
                    TotalCount = totalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                throw;
            }
        }

        public async Task<OrderDto> GetOrderByIdAsync(int id, string userId, bool isAdmin = false)
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
                    .FirstOrDefaultIncAsync<OrderDto>(
                        filters: filters,
                        includes: includes
                    );

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
                    Status = (DTOs.Orders.OrderStatus)order.Status,
                    CreatedAt = order.CreatedAt.Value,
                    UpdatedAt = order.UpdatedAt,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = oi.Product?.Name,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        // TotalPrice is calculated in the entity
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
        public async Task<string> CreateOrderAsync(CreateOrderDto createOrderDto, string userId)
        {
            await _unitOfWork.BeginTransaction();
            var productRepo = _unitOfWork.Repository<Product>();

            try
            {
                // Get user
                var user = await _userManager.FindByIdAsync(userId)
                           ?? throw new ArgumentException("User not found");

                // Create order
                var order = new Order
                {
                    UserId = userId,
                    OrderNumber = GenerateOrderNumber(),
                    Status = (Domain.Entities.OrderStatus)DomainEnums.OrderStatus.Pending,
                    TotalAmount = 0,
                    PaymentStatus = (Domain.Entities.PaymentStatus)DomainEnums.PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                decimal totalAmount = 0;
                var orderItems = new List<OrderItem>();

                // Process each order item
                foreach (var item in createOrderDto.OrderItems)
                {
                    var filters = new List<Expression<Func<Product, bool>>>
                    {
                        p => p.Id == item.SelectedItems.ProductId,
                        p => p.IsActive
                    };

                    var includes = new List<Expression<Func<Product, object>>>
                    {
                        p => p.ProductColours,
                        p => p.ProductColours.Select(a => a.Colour)
                    };

                    var product = await productRepo.FirstOrDefaultIncAsync<Product>(filters, includes);

                    if (product == null)
                        throw new KeyNotFoundException($"Product with ID {item.SelectedItems.ProductId} not found or inactive");

                    // Get selected colour (mandatory)
                    var selectedProductColour = product.ProductColours
                        .FirstOrDefault(a => a.ColourId == item.SelectedItems.ColourId);

                    if (selectedProductColour == null)
                        throw new InvalidOperationException($"Colour with ID {item.SelectedItems.ColourId} not found for product {product.Name}");

                    if (selectedProductColour.Quantity < item.SelectedItems.Quantity)
                        throw new InvalidOperationException(
                            $"Insufficient stock for Colour {selectedProductColour.Colour.Name} for product {product.Name}"
                        );

                    // خصم الكمية من اللون
                    selectedProductColour.Quantity -= item.SelectedItems.Quantity;

                    // تحقق من المخزون الكلي
                    if (product.StockQuantity < item.SelectedItems.Quantity)
                        throw new InvalidOperationException($"Insufficient total stock for product {product.Name}");

                    // خصم من المخزون الكلي
                    product.StockQuantity -= item.SelectedItems.Quantity;

                    // إنشاء OrderItem
                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.SelectedItems.Quantity,
                        UnitPrice = product.Price,
                        ProductColourId = selectedProductColour.Id,
                    };

                    totalAmount += orderItem.TotalPrice;
                    orderItems.Add(orderItem);
                    _unitOfWork.Repository<ProductColours>().Update(selectedProductColour);
                    productRepo.Update(product);
                }

                // Update order total and items
                order.TotalAmount = totalAmount;
                order.OrderItems = orderItems;

                // Save order
                await _unitOfWork.Repository<Order>().AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // Process payment if not Cash
                var paymentResult = new PaymentResultDto();
                if (createOrderDto.PaymentMethod != DTOs.Orders.PaymentMethod.CashOnDelivery)
                {
                    paymentResult = await _paymentService.ProcessPaymentAsync(new ProcessPaymentDto
                    {
                        Merchant_Order_Id = order.OrderNumber,
                        Amount_Cents = ConvertToCents(order.TotalAmount),
                        Auth_Token = "",
                        Currency = "EGP",
                        Delivery_Needed = false,
                        email = user.Email,
                        first_name = user.FirstName,
                        last_name = user.LastName,
                        phone_number = user.PhoneNumber
                        
                    });

                    if (!paymentResult.Success)
                    {
                        await _unitOfWork.Rollback();
                        throw new InvalidOperationException($"Payment failed: {paymentResult.Message}");
                    }

                    // Payment succeeded → set Paid
                    order.PaymentStatus = (Domain.Entities.PaymentStatus)DomainEnums.PaymentStatus.Pending;
                }

                // Update order status
                order.Status = (Domain.Entities.OrderStatus)DomainEnums.OrderStatus.Processing;
                order.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.CommitAsync();
                if (createOrderDto.PaymentMethod != DTOs.Orders.PaymentMethod.CashOnDelivery) return paymentResult.IFrameUrl;
                else return "Succeed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for user {UserId}", userId);
                await _unitOfWork.Rollback();
                throw;
            }
        }

        public static long ConvertToCents(decimal amount)
        {
            // Multiply by 100 and convert to integer (no decimals)
            long amountInCents = (long)(amount * 100);
            return amountInCents;
        }
        private async Task ReturnOrderItemsToStockAsync(Order order)
        {
            var productRepo = _unitOfWork.Repository<Product>();

            foreach (var item in order.OrderItems)
            {
                var product = await productRepo.GetAllAsQueryable().Where(a => a.Id == item.ProductId).Include(a => a.ProductColours).FirstOrDefaultAsync();

                if (product != null)
                {
                    product.StockQuantity += item.Quantity;

                    var productColour = product.ProductColours
                        .FirstOrDefault(pc => pc.Id == item.ProductColourId);

                    if (productColour != null)
                    {
                        productColour.Quantity += item.Quantity;
                    }

                    productRepo.Update(product);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<Order> GetOrderWithIncludesAsync(int id, string userId, bool isAdmin)
        {
            var filters = new List<Expression<Func<Order, bool>>>
            {
                o => o.Id == id
            };

            if (!isAdmin)
            {
                filters.Add(o => o.UserId == userId);
            }

            var includes = new List<Expression<Func<Order, object>>>
            {
                o => o.OrderItems,
                o => o.OrderItems.Select(oi => oi.Product),
                o => o.OrderItems.Select(oi => oi.ProductColour),
            };

            return await _unitOfWork.Repository<Order>()
                .FirstOrDefaultIncAsync<Order>(filters, includes);
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto updateDto, string userId, bool isAdmin)
        {
            try
            {
                var order = await GetOrderWithIncludesAsync(id, userId, isAdmin);
                if (order == null)
                    throw new KeyNotFoundException($"Order with ID {id} not found");

                if (!IsValidStatusTransition(
                    (Domain.Enums.OrderStatus)order.Status,
                    (Domain.Enums.OrderStatus)updateDto.Status))
                {
                    throw new InvalidOperationException(
                        $"Invalid status transition from {order.Status} to {updateDto.Status}");
                }

                order.Status = (Domain.Entities.OrderStatus)updateDto.Status;
                order.UpdatedAt = DateTime.UtcNow;

                if (updateDto.Status.ToString() == Domain.Enums.OrderStatus.Cancelled.ToString())
                {
                    await ReturnOrderItemsToStockAsync(order);
                }

                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.SaveChangesAsync();

                return await GetOrderByIdAsync(order.Id, userId, isAdmin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId}", id);
                throw;
            }
        }

        public async Task<bool> CancelOrderAsync(int id, string userId, bool isAdmin)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var order = await GetOrderWithIncludesAsync(id, userId, isAdmin);
                if (order == null) throw new KeyNotFoundException($"Order with ID {id} not found");

                if (order.Status != Domain.Entities.OrderStatus.Pending && order.Status != Domain.Entities.OrderStatus.Processing)
                    throw new InvalidOperationException($"Cannot cancel order with status {order.Status}");

                order.Status = Domain.Entities.OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                await ReturnOrderItemsToStockAsync(order);

                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", id);
                await _unitOfWork.Rollback();
                throw;
            }
        }

        public async Task<bool> RefundOrderAsync(int orderId, string userId, bool isAdmin)
        {
            await _unitOfWork.BeginTransaction();
            try
            {
                var order = await GetOrderWithIncludesAsync(orderId, userId, isAdmin);
                if (order == null) throw new KeyNotFoundException($"Order with ID {orderId} not found");

                if (order.Status != Domain.Entities.OrderStatus.Delivered && order.Status != Domain.Entities.OrderStatus.Processing)
                    throw new InvalidOperationException($"Cannot refund order with status {order.Status}");

                if (order.PaymentStatus != Domain.Entities.PaymentStatus.Paid)
                    throw new InvalidOperationException("Cannot refund order that hasn't been paid");

                if (order.PaymentMethod == Domain.Entities.PaymentMethod.CashOnDelivery)
                    throw new InvalidOperationException("Cannot refund order paid in Cash On Delivery");

                var refundResult = await _paymentService.ProcessRefundAsync(
                    new ProcessRefundDto
                    {
                        OrderId = order.Id,
                        Amount = order.TotalAmount,
                        TransactionId = order.TransactionId
                    });

                if (!refundResult.Success)
                {
                    _logger.LogWarning("Refund failed for order {OrderId}", orderId);
                    await _unitOfWork.Rollback();
                    return false;
                }

                order.Status = Domain.Entities.OrderStatus.Refunded;
                order.PaymentStatus = Domain.Entities.PaymentStatus.Refunded;
                order.UpdatedAt = DateTime.UtcNow;

                await ReturnOrderItemsToStockAsync(order);

                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Order {OrderId} refunded successfully", orderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding order {OrderId}", orderId);
                await _unitOfWork.Rollback();
                throw;
            }
        }

        public async Task<OrderDto> AddOrderItemAsync(int orderId, AddOrderItemDto itemDto, string userId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultIncAsync<Order>(
                        new List<Expression<Func<Order, bool>>> { o => o.Id == orderId, o => o.UserId == userId },
                        new List<Expression<Func<Order, object>>>
                        {
                            o => o.OrderItems,
                            o => o.OrderItems.Select(oi => oi.Product),
                            o => o.User
                        }
                    );

                if (order == null || order.Status.ToString() != DomainEnums.OrderStatus.Pending.ToString())
                    return null;

                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(itemDto.ProductId);

                if (product == null || product.StockQuantity < itemDto.Quantity)
                    return null;
                var productColour = await _unitOfWork.Repository<ProductColours>()
            .FirstOrDefaultIncAsync<ProductColours>(new List<Expression<Func<ProductColours, bool>>> { pc => pc.ProductId == product.Id});

                if (productColour == null || productColour.Quantity < itemDto.Quantity)
                    return null;

                var orderItem = new OrderItem
                {
                    OrderId = orderId,
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price,
                    ProductColourId = itemDto.ProductColourId
                };

                order.TotalAmount += orderItem.Quantity * orderItem.UnitPrice;
                order.UpdatedAt = DateTime.UtcNow;
                product.StockQuantity -= itemDto.Quantity;
                productColour.Quantity -= itemDto.Quantity;

                await _unitOfWork.Repository<OrderItem>().AddAsync(orderItem);
                _unitOfWork.Repository<Product>().Update(product);
                _unitOfWork.Repository<Order>().Update(order);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new OrderDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    UserId = order.UserId,
                    CustomerEmail = order.User?.Email,
                    OrderTotal = order.TotalAmount,
                    Status = (DTOs.Orders.OrderStatus)order.Status,
                    CreatedAt = order.CreatedAt.Value,
                    UpdatedAt = order.UpdatedAt,
                    OrderItems = order.OrderItems?.Select(oi => new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = oi.Product?.Name,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice
                    }).ToList()
                };
            }
            catch
            {
                await _unitOfWork.Rollback();
                throw;
            }
        }

        public async Task<bool> RemoveOrderItemAsync(int orderId, int itemId, string userId)
        {
            await _unitOfWork.BeginTransaction();

            try
            {
                // Load order with items, products, and colours
                var filters = new List<Expression<Func<Order, bool>>>
                {
                    o => o.Id == orderId,
                    o => o.UserId == userId
                };
                var includes = new List<Expression<Func<Order, object>>>
                {
                    o => o.OrderItems,
                    o => o.OrderItems.Select(oi => oi.Product),
                    o => o.OrderItems.Select(oi => oi.ProductColour),
                    o => o.User
                };
                var order = await _unitOfWork.Repository<Order>().FirstOrDefaultIncAsync<Order>(filters, includes);

                if (order == null || order.Status.ToString() != DomainEnums.OrderStatus.Pending.ToString())
                    return false;

                var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == itemId);
                if (orderItem == null)
                    return false;

                // Update order total
                order.TotalAmount -= orderItem.Quantity * orderItem.UnitPrice;
                order.UpdatedAt = DateTime.UtcNow;

                // Update stock
                if (orderItem.ProductColourId != 0)
                {
                    var productColour = orderItem.ProductColour;
                    if (productColour != null)
                    {
                        productColour.Quantity += orderItem.Quantity;
                        _unitOfWork.Repository<ProductColours>().Update(productColour);
                    }
                }
                else
                {
                    var product = orderItem.Product;
                    if (product != null)
                    {
                        product.StockQuantity += orderItem.Quantity;
                        _unitOfWork.Repository<Product>().Update(product);
                    }
                }

                // Remove order item
                _unitOfWork.Repository<OrderItem>().Delete(orderItem);
                _unitOfWork.Repository<Order>().Update(order);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return true;
            }
            catch
            {
                await _unitOfWork.Rollback();
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
                    o => o.Status.ToString() == DomainEnums.OrderStatus.Pending.ToString()
                };

                // Get order with includes
                var order = await _unitOfWork.Repository<Order>()
                    .FirstOrDefaultIncAsync<OrderDto>(
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
                    merchant_order_id = order.Id,
                    amount_cents = order.TotalAmount,
                    PaymentMethod = order.PaymentMethod.ToString(),
                    PaymentDetails = order.TransactionId ?? order.PaymentMethod.ToString()
                });

                if (paymentResult.Success)
                {
                    // Update order status and payment details
                    order.Status = Domain.Entities.OrderStatus.Processing;
                    order.PaymentStatus = Domain.Entities.PaymentStatus.Paid;
                    order.TransactionId = paymentResult.TransactionId;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Update order
                    _unitOfWork.Repository<Order>().Update(order);
                    await _unitOfWork.SaveChangesAsync();

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
            var filters = new List<Expression<Func<OrderItem, bool>>>
            {
                oi => oi.Id== orderId,
            };
            var includes = new List<Expression<Func<OrderItem, object>>>
            {
                o => o.Product
            };
            var orderItems = await orderItemRepo.FindByIncAsync<OrderItem>(filters, includes);
                

            foreach (var item in orderItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockQuantity += item.Quantity;
                    productRepo.Update(item.Product);
                }
            }
        }
    }
}

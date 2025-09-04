using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using ECommerce.Tests.Services;
using Application.DTOs.Orders;
using Application.Interfaces;
using Domain.Entities;
using ECommerce.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DomainEnums = Domain.Entities;
using DTOs = Application.DTOs.Orders;

namespace ECommerce.Tests.Controllers
{
    public class OrdersControllerTests : IClassFixture<TestFixture>, IAsyncLifetime
    {
        private readonly TestFixture _fixture;
        private readonly AppDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly OrdersController _controller;
        private readonly IPaymentService _paymentService;
        private ApplicationUser _testUser;
        private ApplicationUser _adminUser;

        public OrdersControllerTests(TestFixture fixture)
        {
            _fixture = fixture;
            _dbContext = _fixture.CreateDbContext();
            _userManager = _fixture.GetUserManager();
            
            // Create a test payment service
            _paymentService = new TestPaymentService();
            
            // Create controller with mock logger
            _controller = new OrdersController(
                _dbContext,
                _userManager,
                _paymentService,
                Mock.Of<ILogger<OrdersController>>()
            );
        }
        
        public async Task InitializeAsync()
        {
            // Get or create test users
            _testUser = await _userManager.FindByEmailAsync("test@example.com");
            _adminUser = await _userManager.FindByEmailAsync("admin@example.com");
            
            if (_testUser == null || _adminUser == null)
            {
                throw new Exception("Test users not properly initialized in test fixture");
            }
            
            // Setup controller context with test user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _testUser.Id),
                new Claim(ClaimTypes.Email, _testUser.Email),
                new Claim(ClaimTypes.Name, _testUser.UserName)
            };
            
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
            
            // Clear any existing test data
            _dbContext.Orders.RemoveRange(_dbContext.Orders);
            _dbContext.Products.RemoveRange(_dbContext.Products);
            _dbContext.Categories.RemoveRange(_dbContext.Categories);
            await _dbContext.SaveChangesAsync();
            
            // Seed test data
            await SeedTestDataAsync();
        }

        [Fact]
        public async Task GetOrders_ReturnsUserOrders()
        {
            try
            {
                // Arrange - Create a test order
                await CreateTestOrder();
                
                var filter = new OrderFilterDto
                {
                    PageNumber = 1,
                    PageSize = 10,
                    SortBy = "orderDate",
                    SortDescending = true
                };
                
                // Act
                var result = await _controller.GetOrders(filter);
                
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var orderList = Assert.IsType<OrderListDto>(okResult.Value);
                Assert.NotNull(orderList);
                Assert.Equal(1, orderList.PageNumber);
                Assert.Equal(10, orderList.PageSize);
                Assert.Single(orderList.Items);
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                Console.WriteLine($"Test failed with exception: {ex}");
                throw;
            }
        }

        [Fact]
        public async Task CreateOrder_WithValidCart_CreatesOrder()
        {
            // Arrange - Add items to cart
            var product = await _dbContext.Products.FirstAsync(p => p.StockQuantity > 0);
            var initialStock = product.StockQuantity;
            
            // Add item to cart
            var cart = await _dbContext.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == _testUser.Id) ?? new ShoppingCart { UserId = _testUser.Id };
                
            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                Quantity = 1,
                UnitPrice = product.Price
            });
            
            if (cart.Id == 0)
            {
                _dbContext.ShoppingCarts.Add(cart);
            }
            
            await _dbContext.SaveChangesAsync();

            var createOrderDto = new CreateOrderDto
            {
                ShippingAddress = "123 Test St, Test City",
                PhoneNumber = "+1234567890",
                PaymentMethod = DTOs.PaymentMethod.CreditCard,
                PaymentToken = "test_payment_token"
            };

            // Act
            var result = await _controller.CreateOrder(createOrderDto);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var orderDto = Assert.IsType<OrderDto>(createdAtActionResult.Value);
            
            Assert.Equal(1, orderDto.OrderItems.Count);
            Assert.Equal(product.Price, orderDto.OrderTotal);
            Assert.Equal(Application.DTOs.Orders.OrderStatus.Pending, orderDto.Status);
            Assert.Equal(Application.DTOs.Orders.PaymentMethod.CreditCard, orderDto.PaymentMethod);
            
            // Verify stock was reduced
            var updatedProduct = await _dbContext.Products.FindAsync(product.Id);
            Assert.Equal(initialStock - 1, updatedProduct.StockQuantity);
            
            // Verify cart was cleared
            var userCart = await _dbContext.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == _testUser.Id);
                
            Assert.NotNull(userCart);
            Assert.Empty(userCart.Items);
        }

        [Fact]
        public async Task CreateOrder_WithEmptyCart_ReturnsBadRequest()
        {
            // Arrange
            var createOrderDto = new CreateOrderDto
            {
                ShippingAddress = "123 Test St, Test City",
                PhoneNumber = "+1234567890",
                PaymentMethod = DTOs.PaymentMethod.CashOnDelivery
            };

            // Act
            var result = await _controller.CreateOrder(createOrderDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Your cart is empty", badRequestResult.Value);
        }

        [Fact]
        public async Task GetOrder_WithValidId_ReturnsOrder()
        {
            // Arrange
            var order = await CreateTestOrder();
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetOrder(order.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var orderDto = Assert.IsType<OrderDto>(okResult.Value);
            
            Assert.Equal(order.Id, orderDto.Id);
            Assert.Equal(order.OrderNumber, orderDto.OrderNumber);
            Assert.Equal(order.OrderTotal, orderDto.OrderTotal);
            
            // Verify enum conversions
            Assert.Equal((int)order.Status, (int)orderDto.Status);
            Assert.Equal((int)order.PaymentMethod, (int)orderDto.PaymentMethod);
        }

        [Fact]
        public async Task UpdateOrderStatus_AsAdmin_UpdatesStatus()
        {
            // Arrange - Create a test order
            var order = await CreateTestOrder();
            
            // Switch to admin user
            var adminUser = await _userManager.FindByEmailAsync("admin@example.com");
            var adminClaims = await _userManager.GetClaimsAsync(adminUser);
            var adminIdentity = new System.Security.Claims.ClaimsIdentity(adminClaims, "TestAuthType");
            var adminPrincipal = new System.Security.Claims.ClaimsPrincipal(adminIdentity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = adminPrincipal }
            };

            var updateStatusDto = new UpdateOrderStatusDto
            {
                Status = DTOs.OrderStatus.Processing
            };

            // Act
            var result = await _controller.UpdateOrderStatus(order.Id, updateStatusDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            
            // Verify order status was updated
            var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
            Assert.Equal(DomainEnums.OrderStatus.Processing, updatedOrder.Status);
        }

        [Fact]
        public async Task UpdateOrderStatus_AsNonAdmin_ReturnsForbidden()
        {
            // Arrange - Create a test order
            var order = await CreateTestOrder();
            
            var updateStatusDto = new UpdateOrderStatusDto
            {
                Status = DTOs.OrderStatus.Processing
            };

            // Act - Regular user tries to update status
            var result = await _controller.UpdateOrderStatus(order.Id, updateStatusDto);

            // Assert
            var forbiddenResult = Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task CancelOrder_RefundsPayment_WhenPaid()
        {
            // Arrange - Create a paid order with online payment
            var order = await CreateTestOrder(DTOs.PaymentMethod.CreditCard, DomainEnums.PaymentStatus.Paid);
            
            // Create admin user and add to database
            var adminUser = new ApplicationUser
            {
                UserName = "admin@example.com",
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true
            };
            
            var createAdminResult = await _userManager.CreateAsync(adminUser, "Admin@123");
            if (!createAdminResult.Succeeded)
            {
                throw new Exception("Failed to create admin user: " + 
                    string.Join(", ", createAdminResult.Errors.Select(e => e.Description)));
            }
            
            // Add admin role claim
            var adminClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, adminUser.Id),
                new Claim(ClaimTypes.Email, adminUser.Email),
                new Claim(ClaimTypes.Name, adminUser.UserName),
                new Claim(ClaimTypes.Role, "Admin")
            };
            
            await _userManager.AddClaimsAsync(adminUser, adminClaims);
            
            // Switch to admin user context
            var adminIdentity = new ClaimsIdentity(adminClaims, "TestAuthType");
            var adminPrincipal = new ClaimsPrincipal(adminIdentity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = adminPrincipal }
            };

            var updateStatusDto = new UpdateOrderStatusDto
            {
                Status = Application.DTOs.Orders.OrderStatus.Cancelled
            };

            // Store the current transaction ID to verify it was used in the refund
            var originalTransactionId = order.TransactionId;
            
            // Act
            var result = await _controller.UpdateOrderStatus(order.Id, updateStatusDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            
            // Verify order was cancelled
            var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
            Assert.Equal(DomainEnums.OrderStatus.Cancelled, updatedOrder.Status);
            
            // Since we're using the real TestPaymentService, we can verify the refund was processed
            // by checking the payment status
            var paymentStatus = await _paymentService.GetPaymentStatusAsync(originalTransactionId);
            Assert.True(paymentStatus.Success);
            Assert.Equal("refunded", paymentStatus.Status);
        }

        private async Task SeedTestDataAsync()
        {
            // Create a test category
            var category = new Category
            {
                Name = "Test Category",
                Description = "Test Category Description"
            };
            
            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();
            
            // Create a test product
            var product = new Product
            {
                Name = "Test Product",
                Description = "Test Product Description",
                Price = 100.0m,
                StockQuantity = 10,
                CategoryId = category.Id
            };
            
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
        }
        
        private async Task<Order> CreateTestOrder(
            DTOs.PaymentMethod paymentMethodDto = DTOs.PaymentMethod.CashOnDelivery,
            DomainEnums.PaymentStatus paymentStatus = DomainEnums.PaymentStatus.Pending)
        {
            var product = await _dbContext.Products.FirstAsync();
            
            // Create order with all required properties
            var order = new Order
            {
                UserId = _testUser.Id,
                OrderDate = DateTime.UtcNow,
                Status = DomainEnums.OrderStatus.Pending,
                PaymentStatus = paymentStatus,
                PaymentMethod = (DomainEnums.PaymentMethod)paymentMethodDto,
                TotalAmount = 100.0m,
                ShippingAddress = new Domain.Entities.Address
                {
                    Street = "123 Test St",
                    City = "Test City",
                    State = "Test State",
                    Country = "Test Country",
                    PostalCode = "12345"
                },
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPrice = 100.0m,
                        TotalPrice = 100.0m
                    }
                },
                TransactionId = paymentStatus == DomainEnums.PaymentStatus.Paid ? Guid.NewGuid().ToString() : null
            };
            
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();
            
            return order;
        }
    }
}

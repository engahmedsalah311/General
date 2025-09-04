using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using Application.DTOs.Cart;
using Domain.Entities;
using ECommerce.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerce.Tests.Controllers
{
    public class CartControllerTests : IClassFixture<TestFixture>
    {
        private readonly TestFixture _fixture;
        private readonly Mock<ILogger<CartController>> _loggerMock;
        private readonly CartController _controller;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _dbContext;

        public CartControllerTests(TestFixture fixture)
        {
            _fixture = fixture;
            _loggerMock = new Mock<ILogger<CartController>>();
            _dbContext = _fixture.CreateDbContext();
            _userManager = _fixture.GetUserManager();
            
            _controller = new CartController(_dbContext, _userManager, _loggerMock.Object);
            
            // Set up the user context
            var user = _userManager.FindByEmailAsync("user@example.com").Result;
            var userClaims = _userManager.GetClaimsAsync(user).Result;
            var userIdentity = new System.Security.Claims.ClaimsIdentity(userClaims, "TestAuthType");
            var userPrincipal = new System.Security.Claims.ClaimsPrincipal(userIdentity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userPrincipal }
            };
        }

        [Fact]
        public async Task GetCart_ReturnsUserCart()
        {
            // Act
            var result = await _controller.GetCart();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var cart = Assert.IsType<CartDto>(okResult.Value);
            
            // User should have an empty cart initially
            Assert.Empty(cart.Items);
        }

        [Fact]
        public async Task AddToCart_WithValidProduct_AddsItemToCart()
        {
            // Arrange
            var product = await _dbContext.Products.FirstAsync();
            var addToCartDto = new AddToCartDto
            {
                ProductId = product.Id,
                Quantity = 2
            };

            // Act
            var result = await _controller.AddToCart(addToCartDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var cart = Assert.IsType<CartDto>(okResult.Value);
            
            var cartItem = Assert.Single(cart.Items);
            Assert.Equal(product.Id, cartItem.ProductId);
            Assert.Equal(2, cartItem.Quantity);
            Assert.Equal(product.Price, cartItem.UnitPrice);
            Assert.Equal(product.Price * 2, cartItem.TotalPrice);
        }

        [Fact]
        public async Task AddToCart_WithInsufficientStock_ReturnsBadRequest()
        {
            // Arrange
            var product = await _dbContext.Products.FirstAsync();
            var addToCartDto = new AddToCartDto
            {
                ProductId = product.Id,
                Quantity = product.StockQuantity + 1 // More than available stock
            };

            // Act
            var result = await _controller.AddToCart(addToCartDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("exceeds available stock", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task UpdateCartItem_WithValidData_UpdatesItemQuantity()
        {
            // Arrange - First add an item to the cart
            var product = await _dbContext.Products.FirstAsync();
            var addToCartDto = new AddToCartDto
            {
                ProductId = product.Id,
                Quantity = 1
            };
            
            var addResult = await _controller.AddToCart(addToCartDto);
            var cart = (addResult.Result as OkObjectResult)?.Value as CartDto;
            var cartItem = cart.Items.First();

            // Update the quantity
            var updateCartItemDto = new UpdateCartItemDto
            {
                CartItemId = cartItem.Id,
                Quantity = 3
            };

            // Act
            var result = await _controller.UpdateCartItem(cartItem.Id, updateCartItemDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var updatedCart = Assert.IsType<CartDto>(okResult.Value);
            var updatedItem = Assert.Single(updatedCart.Items);
            
            Assert.Equal(3, updatedItem.Quantity);
            Assert.Equal(product.Price * 3, updatedItem.TotalPrice);
        }

        [Fact]
        public async Task RemoveFromCart_WithValidItem_RemovesItemFromCart()
        {
            // Arrange - First add an item to the cart
            var product = await _dbContext.Products.FirstAsync();
            var addToCartDto = new AddToCartDto
            {
                ProductId = product.Id,
                Quantity = 1
            };
            
            var addResult = await _controller.AddToCart(addToCartDto);
            var cart = (addResult.Result as OkObjectResult)?.Value as CartDto;
            var cartItem = cart.Items.First();

            // Act
            var result = await _controller.RemoveFromCart(cartItem.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var updatedCart = Assert.IsType<CartDto>(okResult.Value);
            
            Assert.Empty(updatedCart.Items);
        }

        [Fact]
        public async Task ClearCart_RemovesAllItemsFromCart()
        {
            // Arrange - Add some items to the cart
            var products = await _dbContext.Products.Take(2).ToListAsync();
            
            foreach (var product in products)
            {
                await _controller.AddToCart(new AddToCartDto
                {
                    ProductId = product.Id,
                    Quantity = 1
                });
            }

            // Act
            var result = await _controller.ClearCart();

            // Assert
            Assert.IsType<NoContentResult>(result);
            
            // Verify cart is empty
            var cart = await _dbContext.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == _userManager.GetUserId(_controller.User));
                
            Assert.NotNull(cart);
            Assert.Empty(cart.Items);
        }
    }
}

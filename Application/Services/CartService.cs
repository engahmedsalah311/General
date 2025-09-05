using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.Cart;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class CartService : ICartService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CartService> _logger;

        public CartService(IUnitOfWork unitOfWork, ILogger<CartService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CartDto> GetCartAsync(string userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                return MapToCartDto(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cart for user {UserId}", userId);
                throw;
            }
        }

        public async Task<CartDto> AddItemToCartAsync(string userId, AddToCartDto addToCartDto)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(addToCartDto.ProductId);

                if (product == null)
                {
                    throw new KeyNotFoundException($"Product with ID {addToCartDto.ProductId} not found");
                }

                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == addToCartDto.ProductId);

                if (existingItem != null)
                {
                    existingItem.Quantity += addToCartDto.Quantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ProductId = addToCartDto.ProductId,
                        Quantity = addToCartDto.Quantity,
                        UnitPrice = product.Price
                    });
                }

                await _unitOfWork.CommitAsync();
                return MapToCartDto(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart for user {UserId}", userId);
                throw;
            }
        }

        public async Task<CartDto> UpdateCartItemAsync(string userId, UpdateCartItemDto updateCartItemDto)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                var item = cart.Items.FirstOrDefault(i => i.Id == updateCartItemDto.CartItemId);

                if (item == null)
                {
                    throw new KeyNotFoundException($"Item with ID {updateCartItemDto.CartItemId} not found in cart");
                }

                if (updateCartItemDto.Quantity <= 0)
                {
                    cart.Items.Remove(item);
                }
                else
                {
                    item.Quantity = updateCartItemDto.Quantity;
                }

                await _unitOfWork.CommitAsync();
                return MapToCartDto(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> RemoveItemFromCartAsync(string userId, int itemId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                var item = cart.Items.FirstOrDefault(i => i.Id == itemId);

                if (item == null)
                {
                    return false;
                }

                cart.Items.Remove(item);
                await _unitOfWork.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ClearCartAsync(string userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                if (cart.Items.Count == 0)
                {
                    return true; // Already empty
                }
                
                cart.Items.Clear();
                await _unitOfWork.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
                return false;
            }
        }

        public async Task<int> GetCartItemCountAsync(string userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                return cart.Items.Sum(i => i.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart item count for user {UserId}", userId);
                throw;
            }
        }

        private async Task<ShoppingCart> GetOrCreateCartAsync(string userId)
        {
            var filters = new List<Expression<Func<ShoppingCart, bool>>>
            {
                c => c.UserId == userId,
            };
            var includes = new List<Expression<Func<ShoppingCart, object>>>
            {
                c => c.Items,
                c => c.Items.Select(a=>a.Product),
            };
           
            var cart = (await _unitOfWork.Repository<ShoppingCart>()
                .FirstOrDefaultIncAsync<ShoppingCart>(
                    filters,includes));

            if (cart == null)
            {
                cart = new ShoppingCart { UserId = userId };
                await _unitOfWork.Repository<ShoppingCart>().AddAsync(cart);
                await _unitOfWork.CommitAsync();
            }

            return cart;
        }

        private CartDto MapToCartDto(ShoppingCart cart)
        {
            return new CartDto
            {
                Id = cart.Id,
                UserId = cart.UserId,
                Items = cart.Items.Select(i => new CartItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "Unknown Product",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    //TotalPrice = i.Quantity * i.UnitPrice
                }).ToList(),
                //TotalPrice = cart.Items.Sum(i => i.Quantity * i.UnitPrice)
            };
        }
    }
}

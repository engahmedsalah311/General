using System.Threading.Tasks;
using Application.DTOs.Cart;
using System.Collections.Generic;

namespace Application.Interfaces
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(string userId);
        Task<CartDto> AddItemToCartAsync(string userId, AddToCartDto addToCartDto);
        Task<CartDto> UpdateCartItemAsync(string userId, UpdateCartItemDto updateCartItemDto);
        Task<bool> RemoveItemFromCartAsync(string userId, int cartItemId);
        Task<bool> ClearCartAsync(string userId);
        Task<int> GetCartItemCountAsync(string userId);
    }
}

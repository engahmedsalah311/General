using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.Auth;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto);
        Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto);
        Task LogoutAsync();
        Task<bool> UserExistsAsync(string email);
        Task<ApplicationUser> GetCurrentUserAsync(ClaimsPrincipal user);
    }
}

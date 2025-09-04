using System.Threading.Tasks;
using API.Controllers;
using Application.DTOs.Auth;
using Application.Services;
using Domain.Entities;
using ECommerce.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerce.Tests.Controllers
{
    public class AuthControllerTests : IClassFixture<TestFixture>
    {
        private readonly TestFixture _fixture;
        private readonly Mock<ILogger<AuthController>> _loggerMock;
        private readonly AuthController _controller;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Infrastructure.Services.JwtService _jwtService;

        public AuthControllerTests(TestFixture fixture)
        {
            _fixture = fixture;
            _loggerMock = new Mock<ILogger<AuthController>>();
            _userManager = _fixture.GetUserManager();
            
            // Configure JWT service
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("JwtSettings:Secret", "very_long_secret_key_that_should_be_at_least_32_bytes_long"),
                    new KeyValuePair<string, string>("JwtSettings:TokenLifetimeMinutes", "60"),
                    new KeyValuePair<string, string>("JwtSettings:RefreshTokenLifetimeDays", "7")
                })
                .Build();
            
            _jwtService = new Infrastructure.Services.JwtService(config);
            var signInManager = _fixture.GetSignInManager(_userManager);
            _controller = new AuthController(_userManager, signInManager, _jwtService, config);
        }

        [Fact]
        public async Task Register_WithValidData_ReturnsSuccessResponse()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!",
                FirstName = "New",
                LastName = "User"
            };

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.NotNull(response.Token);
            Assert.NotNull(response.RefreshToken);
            Assert.Equal(registerDto.Email, response.User.Email);
            Assert.Equal(registerDto.FirstName, response.User.FirstName);
            Assert.Equal(registerDto.LastName, response.User.LastName);
        }

        [Fact]
        public async Task Register_WithExistingEmail_ReturnsBadRequest()
        {
            // Arrange
            var existingUser = await _userManager.FindByEmailAsync("user@example.com");
            var registerDto = new RegisterDto
            {
                Email = "user@example.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!",
                FirstName = "Existing",
                LastName = "User"
            };

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Email is already taken", badRequestResult.Value);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsToken()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "user@example.com",
                Password = "User@123"
            };

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.NotNull(response.Token);
            Assert.NotNull(response.RefreshToken);
            Assert.Equal(loginDto.Email, response.User.Email);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "user@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Invalid login attempt", unauthorizedResult.Value);
        }

        [Fact]
        public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
        {
            // Arrange
            // First, log in to get a valid token
            var loginDto = new LoginDto
            {
                Email = "user@example.com",
                Password = "User@123"
            };
            
            var loginResult = await _controller.Login(loginDto);
            var loginResponse = (loginResult.Result as OkObjectResult)?.Value as AuthResponseDto;
            
            var refreshTokenDto = new RefreshTokenDto
            {
                Token = loginResponse.Token,
                RefreshToken = loginResponse.RefreshToken
            };

            // Act
            var result = await _controller.RefreshToken(refreshTokenDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.NotNull(response.Token);
            Assert.NotNull(response.RefreshToken);
            Assert.NotEqual(loginResponse.Token, response.Token);
            Assert.NotEqual(loginResponse.RefreshToken, response.RefreshToken);
        }

        [Fact]
        public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var refreshTokenDto = new RefreshTokenDto
            {
                Token = "invalid.token.here",
                RefreshToken = "invalid.refresh.token"
            };

            // Act
            var result = await _controller.RefreshToken(refreshTokenDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Invalid token", unauthorizedResult.Value);
        }
    }
}

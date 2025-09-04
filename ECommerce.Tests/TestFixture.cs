using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Domain.Entities;
using Infrastructure.Context;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECommerce.Tests
{
    public class TestFixture : IAsyncLifetime, IDisposable
    {
        private readonly CustomWebApplicationFactory _factory;
        private IServiceScope? _scope;
        private bool _disposed;

        public TestFixture()
        {
            _factory = new CustomWebApplicationFactory();
            _scope = _factory.Services.CreateScope();
        }

        public async Task InitializeAsync()
        {
            // Ensure the database is created and migrated
            var dbContext = CreateDbContext();
            await dbContext.Database.EnsureCreatedAsync();
            
            // Seed test data if needed
            await SeedTestDataAsync();
        }

        public async Task DisposeAsync()
        {
            if (_scope != null)
            {
                var dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.EnsureDeletedAsync();
                _scope.Dispose();
                _scope = null;
            }
            
            _factory.Dispose();
        }

        public HttpClient CreateClient()
        {
            return _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        public IServiceProvider Services => _factory.Services;

        public AppDbContext CreateDbContext()
        {
            return _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        public UserManager<ApplicationUser> GetUserManager()
        {
            return _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        }

        public SignInManager<ApplicationUser> GetSignInManager(UserManager<ApplicationUser> userManager)
        {
            return _scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        }

        public RoleManager<IdentityRole> GetRoleManager()
        {
            return _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        }

        public async Task<AppDbContext> CreateDbContextAsync()
        {
            return await Task.FromResult(_scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task<UserManager<ApplicationUser>> GetUserManagerAsync()
        {
            return await Task.FromResult(_scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        }

        public async Task<RoleManager<IdentityRole>> GetRoleManagerAsync()
        {
            return await Task.FromResult(_scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        }

        public T GetService<T>() where T : class
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _scope?.Dispose();
                    _factory.Dispose();
                }

                _disposed = true;
            }
        }

        private async Task SeedTestDataAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            
            // Seed roles if they don't exist
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }
            
            // Create test user if it doesn't exist
            var testUser = await userManager.FindByEmailAsync("test@example.com");
            if (testUser == null)
            {
                testUser = new ApplicationUser
                {
                    UserName = "testuser",
                    Email = "test@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    EmailConfirmed = true
                };
                
                var result = await userManager.CreateAsync(testUser, "Test@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(testUser, "User");
                }
            }
            
            // Create admin user if it doesn't exist
            var adminUser = await userManager.FindByEmailAsync("admin@example.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@example.com",
                    FirstName = "Admin",
                    LastName = "User",
                    EmailConfirmed = true
                };
                
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            
            // Save changes to the database
            await dbContext.SaveChangesAsync();
        }
    }
}

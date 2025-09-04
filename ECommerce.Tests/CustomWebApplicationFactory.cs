using System;
using System.Linq;
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

namespace ECommerce.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove the app's ApplicationDbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                    options.EnableSensitiveDataLogging();
                });

                // Build the service provider
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database context
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<AppDbContext>();
                    var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

                    try
                    {
                        // Ensure the database is created
                        db.Database.EnsureCreated();

                        // Seed the database with test data
                        SeedTestData(db, scopedServices).Wait();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the database. Error: {Message}", ex.Message);
                    }
                }
            });
        }

        private static async Task SeedTestData(AppDbContext dbContext, IServiceProvider serviceProvider)
        {
            // Get required services
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

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

            // Create test category if it doesn't exist
            if (!dbContext.Categories.Any())
            {
                var category = new Category
                {
                    Name = "Test Category",
                    Description = "Test Category Description"
                };
                dbContext.Categories.Add(category);
                await dbContext.SaveChangesAsync();

                // Create test product
                var product = new Product
                {
                    Name = "Test Product",
                    Description = "Test Product Description",
                    Price = 100.0m,
                    StockQuantity = 10,
                    CategoryId = category.Id
                };
                dbContext.Products.Add(product);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

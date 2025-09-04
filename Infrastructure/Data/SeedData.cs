using Domain.Entities;
using Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Seed roles
            string[] roles = { "Admin", "User" };

            foreach (var role in roles)
            {
                var roleExists = await roleManager.RoleExistsAsync(role);
                if (!roleExists)
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Create admin user
            var adminEmail = "admin@example.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    EmailConfirmed = true
                };

                var createAdmin = await userManager.CreateAsync(admin, "Admin@123");
                if (createAdmin.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }

            // Seed categories if none exist
            if (!context.Categories.Any())
            {
                var categories = new[]
                {
                    new Category { Name = "Electronics", Description = "Electronic devices and accessories" },
                    new Category { Name = "Clothing", Description = "Men's and women's clothing" },
                    new Category { Name = "Books", Description = "Fiction and non-fiction books" },
                    new Category { Name = "Home & Kitchen", Description = "Home and kitchen appliances" },
                    new Category { Name = "Sports & Outdoors", Description = "Sports equipment and outdoor gear" }
                };

                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();

                // Seed sample products if no products exist
                if (!context.Products.Any())
                {
                    var electronicsId = categories.First(c => c.Name == "Electronics").Id;
                    var booksId = categories.First(c => c.Name == "Books").Id;

                    var products = new[]
                    {
                        new Product 
                        { 
                            Name = "Wireless Headphones", 
                            Description = "High-quality wireless headphones with noise cancellation",
                            Price = 99.99m, 
                            StockQuantity = 50,
                            CategoryId = electronicsId,
                            IsBestSeller = true
                        },
                        new Product 
                        { 
                            Name = "Smartphone Stand", 
                            Description = "Adjustable stand for smartphones and tablets",
                            Price = 19.99m, 
                            StockQuantity = 100,
                            CategoryId = electronicsId
                        },
                        new Product 
                        { 
                            Name = "The Great Novel", 
                            Description = "Bestselling fiction book by a renowned author",
                            Price = 14.99m, 
                            StockQuantity = 200,
                            CategoryId = booksId,
                            IsBestSeller = true
                        }
                    };

                    await context.Products.AddRangeAsync(products);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}

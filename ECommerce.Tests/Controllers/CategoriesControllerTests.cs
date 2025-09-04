using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using Application.DTOs.Categories;
using Domain.Entities;
using ECommerce.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerce.Tests.Controllers
{
    public class CategoriesControllerTests : IClassFixture<TestFixture>
    {
        private readonly TestFixture _fixture;
        private readonly CategoriesController _controller;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesControllerTests(TestFixture fixture)
        {
            _fixture = fixture;
            _dbContext = _fixture.CreateDbContextAsync().Result;
            _logger = Mock.Of<ILogger<CategoriesController>>();
            _controller = new CategoriesController(_dbContext, _logger);
            
            // Clear the database before each test
            _dbContext.Categories.RemoveRange(_dbContext.Categories);
            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task GetCategories_ReturnsListOfCategories()
        {
            // Arrange
            var testCategories = new List<Category>
            {
                new Category { Id = 1, Name = "Test Category 1", Description = "Test Description 1" },
                new Category { Id = 2, Name = "Test Category 2", Description = "Test Description 2" }
            };

            _dbContext.Categories.AddRange(testCategories);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetCategories();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<Application.DTOs.Categories.CategoryDto>>>(result);
            var returnValue = Assert.IsType<List<Application.DTOs.Categories.CategoryDto>>(actionResult.Value);
            Assert.Equal(2, returnValue.Count);
        }
        
        [Fact]
        public async Task GetCategory_WithValidId_ReturnsCategory()
        {
            // Arrange
            var testCategory = new Category { Id = 1, Name = "Test Category", Description = "Test Description" };
            _dbContext.Categories.Add(testCategory);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetCategory(1);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Application.DTOs.Categories.CategoryDto>>(result);
            var returnValue = actionResult.Value ?? throw new NullReferenceException("Category DTO is null");
            Assert.Equal(testCategory.Name, returnValue.Name);
            Assert.Equal(testCategory.Description, returnValue.Description);
        }
    }
}

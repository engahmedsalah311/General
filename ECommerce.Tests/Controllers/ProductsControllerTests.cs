using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using Application.DTOs.Products;
using Application.Interfaces;
using Domain.Entities;
using ECommerce.Tests;
using Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerce.Tests.Controllers
{
    public class ProductsControllerTests : IDisposable
    {
        private readonly Mock<IProductService> _mockProductService;
        private readonly Mock<ILogger<ProductsController>> _mockLogger;
        private readonly ProductsController _controller;
        private bool _disposed = false;

        public ProductsControllerTests()
        {
            _mockProductService = new Mock<IProductService>();
            _mockLogger = new Mock<ILogger<ProductsController>>();
            _controller = new ProductsController(_mockProductService.Object, _mockLogger.Object);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private readonly List<ProductDto> _testProducts = new()
        {
            new ProductDto { Id = 1, Name = "Laptop", Description = "High performance laptop", Price = 999.99m, CategoryId = 1, StockQuantity = 10, IsActive = true },
            new ProductDto { Id = 2, Name = "Smartphone", Description = "Latest smartphone", Price = 699.99m, CategoryId = 1, StockQuantity = 15, IsActive = true },
            new ProductDto { Id = 3, Name = "Headphones", Description = "Noise cancelling", Price = 199.99m, CategoryId = 1, StockQuantity = 20, IsActive = true }
        };

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        [Fact]
        public async Task GetProducts_ReturnsAllProducts()
        {
            // Arrange
            var testProducts = new List<ProductDto>
            {
                new ProductDto { Id = 1, Name = "Test Product 1", Price = 10.99m },
                new ProductDto { Id = 2, Name = "Test Product 2", Price = 20.99m },
                new ProductDto { Id = 3, Name = "Test Product 3", Price = 30.99m }
            };

            _mockProductService.Setup(s => s.GetAllAsync())
                .ReturnsAsync(testProducts);

            // Act
            var result = await _controller.GetAll();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ProductDto>>>(result);
            var returnValue = Assert.IsType<OkObjectResult>(actionResult.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<ProductDto>>(returnValue.Value);
            Assert.Equal(3, products.Count());
            _mockProductService.Verify(s => s.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetProductsByCategory_WithValidCategory_ReturnsProducts()
        {
            // Arrange
            var categoryId = 1;
            var expectedProducts = _testProducts.Where(p => p.CategoryId == categoryId).ToList();
            
            _mockProductService.Setup(s => s.GetProductsByCategoryAsync(categoryId))
                .ReturnsAsync(expectedProducts);

            // Act
            var result = await _controller.GetProductsByCategory(categoryId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ProductDto>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<ProductDto>>(okResult.Value);
            
            Assert.NotNull(products);
            Assert.Equal(3, products.Count());
            Assert.All(products, p => Assert.Equal(categoryId, p.CategoryId));
            _mockProductService.Verify(s => s.GetProductsByCategoryAsync(categoryId), Times.Once);
        }

        [Fact]
        public async Task GetProductsByCategory_WithInvalidCategory_ReturnsEmptyList()
        {
            // Arrange
            var invalidCategoryId = 999;
            var emptyList = new List<ProductDto>();
            
            _mockProductService.Setup(s => s.GetProductsByCategoryAsync(invalidCategoryId))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _controller.GetProductsByCategory(invalidCategoryId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ProductDto>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<ProductDto>>(okResult.Value);
            
            Assert.NotNull(products);
            Assert.Empty(products);
            _mockProductService.Verify(s => s.GetProductsByCategoryAsync(invalidCategoryId), Times.Once);
        }

        [Fact]
        public async Task GetFilteredProducts_WithFilters_ReturnsFilteredProducts()
        {
            // Arrange
            var filter = new ProductFilterDto
            {
                SearchTerm = "laptop",
                MinPrice = 500,
                MaxPrice = 1500,
                CategoryId = 1,
                PageNumber = 1,
                PageSize = 10
            };

            var expectedResult = new ProductListDto
            {
                Items = new List<ProductDto> { _testProducts[0] },
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 10
            };

            _mockProductService.Setup(s => s.GetFilteredProductsAsync(It.IsAny<ProductFilterDto>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GetFilteredProducts(filter);

            // Assert
            var actionResult = Assert.IsType<ActionResult<ProductListDto>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var productList = Assert.IsType<ProductListDto>(okResult.Value);
            
            Assert.NotNull(productList);
            Assert.Single(productList.Items);
            Assert.Equal("Laptop", productList.Items.First().Name);
            _mockProductService.Verify(s => s.GetFilteredProductsAsync(
                It.Is<ProductFilterDto>(f => 
                    f.SearchTerm == filter.SearchTerm &&
                    f.MinPrice == filter.MinPrice &&
                    f.MaxPrice == filter.MaxPrice &&
                    f.CategoryId == filter.CategoryId)), 
                Times.Once);
        }

        [Fact]
        public async Task GetProduct_WithValidId_ReturnsProduct()
        {
            // Arrange
            var testId = 1;
            var testProduct = new ProductDto { Id = testId, Name = "Test Product", Price = 10.99m };

            _mockProductService.Setup(s => s.GetByIdAsync(testId))
                .ReturnsAsync(testProduct);

            // Act
            var result = await _controller.GetById(testId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<ProductDto>>(result);
            var returnValue = Assert.IsType<OkObjectResult>(actionResult.Result);
            var product = Assert.IsType<ProductDto>(returnValue.Value);
            Assert.Equal(testId, product.Id);
            _mockProductService.Verify(s => s.GetByIdAsync(testId), Times.Once);
        }

        [Fact]
        public async Task GetProduct_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = -1;
            _mockProductService.Setup(s => s.GetByIdAsync(invalidId))
                .ReturnsAsync((ProductDto)null);

            // Act
            var result = await _controller.GetById(invalidId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<ProductDto>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
            _mockProductService.Verify(s => s.GetByIdAsync(invalidId), Times.Once);
        }

        [Fact]
        public async Task CreateProduct_WithValidData_ReturnsCreatedProduct()
        {
            // Arrange
            var createDto = new CreateProductDto
            {
                Name = "New Product",
                Description = "New Description",
                Price = 15.99m,
                StockQuantity = 10,
                CategoryId = 1,
                IsActive = true
            };

            var expectedProduct = new ProductDto
            {
                Id = 1,
                Name = createDto.Name,
                Description = createDto.Description,
                Price = createDto.Price,
                StockQuantity = createDto.StockQuantity,
                CategoryId = createDto.CategoryId,
                IsActive = createDto.IsActive
            };

            _mockProductService.Setup(s => s.AddAsync(It.IsAny<CreateProductDto>()))
                .ReturnsAsync(expectedProduct);

            // Act
            var result = await _controller.Create(createDto);

            // Assert
            var actionResult = Assert.IsType<ActionResult<ProductDto>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var product = Assert.IsType<ProductDto>(createdAtActionResult.Value);
            
            Assert.Equal(expectedProduct.Name, product.Name);
            Assert.Equal(expectedProduct.Price, product.Price);
            Assert.Equal(expectedProduct.Id, product.Id);
            _mockProductService.Verify(s => s.AddAsync(It.Is<CreateProductDto>(dto => 
                dto.Name == createDto.Name && 
                dto.Price == createDto.Price &&
                dto.CategoryId == createDto.CategoryId)), Times.Once);
            Assert.Equal(expectedProduct.Price, product.Price);
            Assert.Equal(expectedProduct.Id, product.Id);
            
            _mockProductService.Verify(s => s.AddAsync(It.IsAny<CreateProductDto>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_WithValidData_ReturnsNoContent()
        {
            // Arrange
            var productId = 1;
            var updateDto = new UpdateProductDto
            {
                Name = "Updated Product",
                Description = "Updated Description",
                Price = 19.99m,
                StockQuantity = 5,
                CategoryId = 2,
                IsActive = true
            };

            _mockProductService.Setup(s => s.UpdateAsync(productId, It.IsAny<UpdateProductDto>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Update(productId, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockProductService.Verify(s => s.UpdateAsync(
                productId, 
                It.Is<UpdateProductDto>(dto => 
                    dto.Name == updateDto.Name && 
                    dto.Price == updateDto.Price &&
                    dto.CategoryId == updateDto.CategoryId)), 
                Times.Once);
        }

        [Fact]
        public async Task DeleteProduct_WithValidId_ReturnsNoContent()
        {
            // Arrange
            using var dbContext = _fixture.CreateDbContext();
            var product = new Product
            {
                Name = "Product to Delete",
                Description = "Will be deleted",
                Price = 9.99m,
                StockQuantity = 1,
                CategoryId = (await dbContext.Categories.FirstAsync()).Id,
                IsActive = true
            };
            
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteProduct(product.Id);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Null(await dbContext.Products.FindAsync(product.Id));
        }
    }
}

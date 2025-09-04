using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.Products;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IUnitOfWork unitOfWork, ILogger<ProductService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<ProductDto>> GetAllAsync()
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> 
                { 
                    p => p.Category 
                };
                
                // Use FindByIncMappedAsync to get all products with category included
                var products = await _unitOfWork.Repository<Product>()
                    .FindByIncMappedAsync<ProductDto>(
                        filters: null,
                        includes: includes
                    );

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all products");
                throw;
            }
        }

        public async Task<ProductDto> GetByIdAsync(int id)
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> 
                { 
                    p => p.Category 
                };
                
                var filters = new List<Expression<Func<Product, bool>>> 
                { 
                    p => p.Id == id 
                };
                
                var product = await _unitOfWork.Repository<Product>()
                    .FirstOrDefaultIncAsync(
                        filters: filters,
                        includes: includes
                    );

                if (product == null)
                {
                    throw new KeyNotFoundException($"Product with ID {id} not found");
                }

                return new ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    ImageUrl = product.ImageUrl,
                    StockQuantity = product.StockQuantity,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category?.Name,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt,
                    UpdatedAt = product.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product with ID {ProductId}", id);
                throw;
            }
        }

        public async Task<ProductDto> AddAsync(ProductDto productDto)
        {
            try
            {
                var product = new Product
                {
                    Name = productDto.Name,
                    Description = productDto.Description,
                    Price = productDto.Price,
                    ImageUrl = productDto.ImageUrl,
                    StockQuantity = productDto.StockQuantity,
                    CategoryId = productDto.CategoryId,
                    IsActive = productDto.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Product>().AddAsync(product);
                await _unitOfWork.CommitAsync();

                // Refresh the product to get the category name
                return await GetByIdAsync(product.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product");
                throw;
            }
        }

        public async Task<bool> UpdateAsync(int id, ProductDto productDto)
        {
            try
            {
                // Get the existing product
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id);
                if (product == null)
                {
                    return false;
                }

                // Update product properties
                product.Name = productDto.Name ?? product.Name;
                product.Description = productDto.Description ?? product.Description;
                product.Price = productDto.Price;
                product.ImageUrl = productDto.ImageUrl ?? product.ImageUrl;
                product.StockQuantity = productDto.StockQuantity;
                product.CategoryId = productDto.CategoryId;
                product.IsActive = productDto.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                // Update the product using repository
                _unitOfWork.Repository<Product>().Update(product);
                await _unitOfWork.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID {ProductId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                // Find the product to delete
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id);
                if (product == null)
                {
                    return false;
                }

                // Soft delete by marking as inactive
                product.IsActive = false;
                product.UpdatedAt = DateTime.UtcNow;
                
                // Update the product instead of hard delete
                _unitOfWork.Repository<Product>().Update(product);
                await _unitOfWork.CommitAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product with ID {ProductId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetActiveProductsAsync()
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> 
                { 
                    p => p.Category 
                };
                
                var filters = new List<Expression<Func<Product, bool>>> 
                { 
                    p => p.IsActive 
                };
                
                var products = await _unitOfWork.Repository<Product>()
                    .FindByIncMappedAsync<ProductDto>(
                        filters: filters,
                        includes: includes
                    );

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active products");
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(string category)
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> 
                { 
                    p => p.Category 
                };
                
                var filters = new List<Expression<Func<Product, bool>>> 
                { 
                    p => p.IsActive && 
                         p.Category != null && 
                         p.Category.Name.ToLower() == category.ToLower()
                };
                
                var products = await _unitOfWork.Repository<Product>()
                    .FindByIncMappedAsync<ProductDto>(
                        filters: filters,
                        includes: includes
                    );

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for category {Category}", category);
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice)
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> 
                { 
                    p => p.Category 
                };
                
                var filters = new List<Expression<Func<Product, bool>>> 
                { 
                    p => p.IsActive && 
                         p.Price >= minPrice && 
                         p.Price <= maxPrice
                };
                
                var products = await _unitOfWork.Repository<Product>()
                    .FindByIncMappedAsync<ProductDto>(
                        filters: filters,
                        includes: includes
                    );

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products in price range {MinPrice} to {MaxPrice}", minPrice, maxPrice);
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetPagedItemAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> 
                { 
                    p => p.Category 
                };
                
                var filters = new List<Expression<Func<Product, bool>>> 
                { 
                    p => p.IsActive 
                };
                
                var result = await _unitOfWork.Repository<Product>()
                    .GetPagedMappedAsync<ProductDto>(
                        page: pageNumber,
                        pageSize: pageSize,
                        filters: filters,
                        includes: includes,
                        orderBy: q => q.OrderBy(p => p.Name)
                    );

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged products");
                throw;
            }
        }

        public async Task<IEnumerable<ProductDto>> GetPagedItemAsync(Expression<Func<ProductDto, bool>> predicate, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var allProducts = await _unitOfWork.Repository<Product>()
                    .GetAll()
                    .Include(p => p.Category)
                    .Where(p => p.IsActive)
                    .Select(p => MapToDto(p))
                    .ToListAsync();

                return allProducts
                    .AsQueryable()
                    .Where(predicate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving filtered paged products");
                throw;
            }
        }

        public async Task<ProductListDto> GetFilteredProductsAsync(ProductFilterDto filter)
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> { p => p.Category };
                var filters = new List<Expression<Func<Product, bool>>> { p => !p.IsDeleted };

                // Apply search term filter
                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    var searchTerm = filter.SearchTerm.ToLower();
                    filters.Add(p => p.Name.ToLower().Contains(searchTerm) || 
                                   (p.Description != null && p.Description.ToLower().Contains(searchTerm)));
                }

                // Apply category filter
                if (filter.CategoryId.HasValue)
                {
                    filters.Add(p => p.CategoryId == filter.CategoryId.Value);
                }

                // Apply price range filters
                if (filter.MinPrice.HasValue)
                {
                    filters.Add(p => p.Price >= filter.MinPrice.Value);
                }

                if (filter.MaxPrice.HasValue)
                {
                    filters.Add(p => p.Price <= filter.MaxPrice.Value);
                }

                // Apply bestseller filter
                if (filter.IsBestSeller.HasValue)
                {
                    filters.Add(p => p.IsBestSeller == filter.IsBestSeller.Value);
                }

                // Apply in-stock filter
                if (filter.InStock.HasValue && filter.InStock.Value)
                {
                    filters.Add(p => p.StockQuantity > 0);
                }

                // Apply sorting
                var sortOrder = filter.SortOrder?.ToLower() ?? "";
                var orderBy = sortOrder switch
                {
                    "price_asc" => (Expression<Func<Product, object>>)(p => p.Price),
                    "price_desc" => (Expression<Func<Product, object>>)(p => p.Price),
                    "name_asc" => p => p.Name,
                    "name_desc" => (Expression<Func<Product, object>>)(p => p.Name),
                    "newest" => (Expression<Func<Product, object>>)(p => p.CreatedAt),
                    _ => p => p.Name
                };

                var isDescending = sortOrder switch
                {
                    "price_desc" => true,
                    "name_desc" => true,
                    _ => false
                };

                // Use the repository's search functionality
                var result = await _unitOfWork.Repository<Product>()
                    .SearchPagedMappedAsync<ProductDto, ProductFilterDto>(
                        filter,
                        filter.PageNumber,
                        filter.PageSize,
                        includes);

                return new ProductListDto
                {
                    Items = result.Data,
                    TotalCount = result.TotalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving filtered products");
                throw;
            }
        }

        private ProductDto MapToDto(Product product)
        {
            if (product == null)
                return null;

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
        }
    }
}

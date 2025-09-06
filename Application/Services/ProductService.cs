using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.ProductColours;
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
                    .FirstOrDefaultIncAsync<ProductDto>(
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
                    CreatedAt = product.CreatedAt.Value,
                    LastUpdated = product.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product with ID {ProductId}", id);
                throw;
            }
        }

        public async Task<ProductDto> AddAsync(CreateProductDto productDto)
        {
            try
            {
                var validColourIds = await _unitOfWork.Repository<Colour>()
                                                      .GetAllAsQueryable()
                                                      .Where(c => productDto.Colours.Select(pc => pc.ColourId).Contains(c.Id))
                                                      .Select(c => c.Id)
                                                      .ToListAsync();
                var product = new Product
                {
                    Name = productDto.Name,
                    Description = productDto.Description,
                    Price = productDto.Price,
                    ImageUrl = productDto.ImageUrl,
                    StockQuantity = productDto.StockQuantity,
                    CategoryId = productDto.CategoryId,
                    IsActive = productDto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    IsBestSeller = productDto.IsBestSeller,
                    ProductColours = productDto.Colours
                    .Where(pc => validColourIds.Contains(pc.ColourId))
                    .Select(pc => new ProductColours
                    {
                        ColourId = pc.ColourId,
                        Quantity = pc.Quantity
                    })
                    .ToList()
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
        public async Task<bool> UpdateAsync(int id, CreateProductDto productDto)
        {
            try
            {
                var product = await _unitOfWork.Repository<Product>()
                    .GetAllAsQueryable()
                    .Include(p => p.ProductColours)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                    return false;

                // --- Update Product fields ---
                product.Name = productDto.Name ?? product.Name;
                product.Description = productDto.Description ?? product.Description;
                product.Price = productDto.Price;
                product.ImageUrl = productDto.ImageUrl ?? product.ImageUrl;
                product.StockQuantity = productDto.StockQuantity;
                product.CategoryId = productDto.CategoryId;
                product.IsActive = productDto.IsActive;
                product.UpdatedAt = DateTime.UtcNow;
                product.IsBestSeller = productDto.IsBestSeller;

                // --- Sync Colours + Quantities ---
                var oldColours = product.ProductColours.ToList();
                var newColours = productDto.Colours ?? new List<CreateProductColourDto>();

                var newColourIds = newColours.Select(c => c.ColourId).ToList();

                // 1. Add or update colours
                foreach (var newColour in newColours)
                {
                    var existing = oldColours.FirstOrDefault(pc => pc.ColourId == newColour.ColourId);
                    if (existing == null)
                    {
                        // Add new colour
                        product.ProductColours.Add(new ProductColours
                        {
                            ProductId = product.Id,
                            ColourId = newColour.ColourId,
                            Quantity = newColour.Quantity
                        });
                    }
                    else
                    {
                        // Update quantity if changed
                        if (existing.Quantity != newColour.Quantity)
                        {
                            existing.Quantity = newColour.Quantity;
                        }
                    }
                }

                // 2. Remove deleted colours
                var toRemove = oldColours.Where(pc => !newColourIds.Contains(pc.ColourId)).ToList();
                foreach (var item in toRemove)
                {
                    product.ProductColours.Remove(item);
                    _unitOfWork.Repository<ProductColours>().Delete(item);
                }

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
        public async Task<ProductListDto> GetFilteredProductsAsync(ProductFilterDto filter)
        {
            try
            {
                var includes = new List<Expression<Func<Product, object>>> { p => p.Category };
                var filters = new List<Expression<Func<Product, bool>>> { p => !p.IsActive };

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

        
    }
}

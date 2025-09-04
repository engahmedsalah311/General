using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.Categories;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(IUnitOfWork unitOfWork, ILogger<CategoryService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
        {
            try
            {
                var filters = new List<Expression<Func<Category, bool>>>
                {
                    c => c.IsActive
                };

                var includes = new List<Expression<Func<Category, object>>>
                {
                    c => c.Products
                };

                var categories = await _unitOfWork.Repository<Category>()
                    .FindByIncMappedAsync<CategoryDto>(
                        filters: filters,
                        includes: includes
                    );

                // Map to DTO with product count
                var result = categories.Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ProductCount = c.Products?.Count ?? 0
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all categories");
                throw;
            }
        }

        public async Task<CategoryDto> GetCategoryByIdAsync(int id)
        {
            try
            {
                var filters = new List<Expression<Func<Category, bool>>>
                {
                    c => c.Id == id && c.IsActive
                };

                var includes = new List<Expression<Func<Category, object>>>
                {
                    c => c.Products
                };

                var category = await _unitOfWork.Repository<Category>()
                    .FirstOrDefaultIncAsync(
                        filters: filters,
                        includes: includes
                    );

                if (category == null)
                {
                    return null;
                }

                return new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    IsActive = category.IsActive,
                    ProductCount = category.Products?.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category with ID {CategoryId}", id);
                throw;
            }
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
        {
            try
            {
                var category = new Category
                {
                    Name = createCategoryDto.Name,
                    Description = createCategoryDto.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Category>().AddAsync(category);
                await _unitOfWork.CommitAsync();

                return new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    IsActive = category.IsActive,
                    ProductCount = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                throw;
            }
        }

        public async Task UpdateCategoryAsync(UpdateCategoryDto updateCategoryDto)
        {
            try
            {
                var category = await _unitOfWork.Repository<Category>()
                    .GetByIdAsync(updateCategoryDto.Id);

                if (category == null)
                {
                    throw new KeyNotFoundException($"Category with ID {updateCategoryDto.Id} not found");
                }

                category.Name = updateCategoryDto.Name ?? category.Name;
                category.Description = updateCategoryDto.Description ?? category.Description;
                category.IsActive = updateCategoryDto.IsActive;
                category.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Repository<Category>().Update(category);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category with ID {CategoryId}", updateCategoryDto.Id);
                throw;
            }
        }

        public async Task DeleteCategoryAsync(int id)
        {
            try
            {
                var filters = new List<Expression<Func<Category, bool>>>
                {
                    c => c.Id == id
                };

                var includes = new List<Expression<Func<Category, object>>>
                {
                    c => c.Products
                };

                var category = await _unitOfWork.Repository<Category>()
                    .FirstOrDefaultIncAsync(filters, includes);

                if (category == null)
                {
                    throw new KeyNotFoundException($"Category with ID {id} not found");
                }

                if (category.Products?.Any() == true)
                {
                    throw new InvalidOperationException("Cannot delete category with associated products");
                }

                _unitOfWork.Repository<Category>().Delete(category);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category with ID {CategoryId}", id);
                throw;
            }
        }

        public async Task<bool> CategoryExistsAsync(int id)
        {
            try
            {
                var filters = new List<Expression<Func<Category, bool>>>
                {
                    c => c.Id == id && c.IsActive
                };

                return await _unitOfWork.Repository<Category>()
                    .AnyAsync(filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category with ID {CategoryId} exists", id);
                throw;
            }
        }

        public async Task<bool> CategoryNameExistsAsync(string name, int? excludeId = null)
        {
            try
            {
                var filters = new List<Expression<Func<Category, bool>>>
                {
                    c => c.Name.ToLower() == name.ToLower() && c.IsActive
                };

                if (excludeId.HasValue)
                {
                    filters.Add(c => c.Id != excludeId.Value);
                }

                return await _unitOfWork.Repository<Category>()
                    .AnyAsync(filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category with name {CategoryName} exists", name);
                throw;
            }
        }
    }
}

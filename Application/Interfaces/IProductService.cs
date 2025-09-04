using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.Products;

namespace Application.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetAllAsync();
        Task<ProductDto> GetByIdAsync(int id);
        Task<ProductDto> AddAsync(ProductDto productDto);
        Task<bool> UpdateAsync(int id, ProductDto productDto);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<ProductDto>> GetActiveProductsAsync();
        Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(string category);
        Task<IEnumerable<ProductDto>> GetProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice);
        Task<IEnumerable<ProductDto>> GetPagedItemAsync(int pageNumber = 1, int pageSize = 10);
        Task<IEnumerable<ProductDto>> GetPagedItemAsync(Expression<Func<ProductDto, bool>> predicate, int pageNumber = 1, int pageSize = 10);
        
        /// <summary>
        /// Gets filtered, sorted, and paginated products based on filter criteria
        /// </summary>
        /// <param name="filter">Filter criteria for products</param>
        /// <returns>Paginated list of products matching the filter criteria</returns>
        Task<ProductListDto> GetFilteredProductsAsync(ProductFilterDto filter);
    }
}

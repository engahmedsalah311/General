using Application.DTOs;
using Application.Interfaces;
using Application.Parameters;
using Application.Wrapper;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IProductService : IGenericService<Product, ProductDto>
    {
        Task<Result<IEnumerable<ProductDto>>> GetAvailableProductsAsync();
        Task<Result<IEnumerable<ProductDto>>> GetProductsByCategoryAsync(int category);
        Task<Result<IEnumerable<ProductDto>>> GetProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice);
        Task<Result<Pagination<ProductDto>>> Search(SearchProductParameters parameters);
        Task<IEnumerable<Product>> SearchProductsAsync(SearchProductParameters dto);
        Task<Result<Pagination<Product>>> SearchAsyncProductsPaged(SearchProductParameters parameters);
        Task<Result<Pagination<ProductDto>>> SearchAsyncProductsPagedMapped(SearchProductParameters parameters);
    }
}

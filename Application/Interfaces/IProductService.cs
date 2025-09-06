using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.DTOs.Products;

namespace Application.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto> GetByIdAsync(int id);
        Task<ProductDto> AddAsync(CreateProductDto productDto);
        Task<bool> UpdateAsync(int id, CreateProductDto productDto);
        Task<bool> DeleteAsync(int id);
        Task<ProductListDto> GetFilteredProductsAsync(ProductFilterDto filter);
    }
}

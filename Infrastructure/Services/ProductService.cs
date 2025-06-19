using Application.DTOs;
using Application.Interfaces;
using Application.Parameters;
using Application.Wrapper;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Infrastructure.Services
{
    public class ProductService : GenericService<Product, ProductDto>, IProductService
    {
        public ProductService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        public async Task<Result<IEnumerable<ProductDto>>> GetAvailableProductsAsync() 
        {
            try
            {
                var products = await _repository.GetAllAsQueryable()
                    .Where(p => p.IsAvailable && p.StockQuantity > 0)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<ProductDto>>(products);
                return Result<IEnumerable<ProductDto>>.SuccessResult(dtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<ProductDto>>.FailureResult(ex.Message);
            }
        }

        public async Task<Result<IEnumerable<ProductDto>>> GetProductsByCategoryAsync(int category)
        {
            try
            {
                var products = await _repository.GetAllAsQueryable()
                    .Where(p => p.CategoryId == category)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<ProductDto>>(products);
                return Result<IEnumerable<ProductDto>>.SuccessResult(dtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<ProductDto>>.FailureResult(ex.Message);
            }
        }

        public async Task<Result<IEnumerable<ProductDto>>> GetProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice)
        {
            try
            {
                var products = await _repository.GetAllAsQueryable()
                    .Where(p => p.Price >= minPrice && p.Price <= maxPrice)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<ProductDto>>(products);
                return Result<IEnumerable<ProductDto>>.SuccessResult(dtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<ProductDto>>.FailureResult(ex.Message);
            }
        }

        public async Task<Result<Pagination<ProductDto>>> Search(SearchProductParameters parameters)
        {
            var filters = new List<Expression<Func<Product, bool>>>();

            if (!string.IsNullOrEmpty(parameters.Name))
                filters.Add(p => p.Name.Contains(parameters.Name));

            if (!string.IsNullOrEmpty(parameters.Description))
                filters.Add(p => p.Name.Contains(parameters.Description));

            //if (parameters..HasValue)
            //    filters.Add(p => p.Price <= parameters.MaxPrice.Value);
            var includes = new List<Expression<Func<Product, object>>>
            {
                p => p.Category
            };

            var (data, totalCount) = await _unitOfWork.Repository<Product>()
                .GetPagedMappedAsync<ProductDto>(parameters.PageNumber,parameters.PageSize,filters,includes);

            var result = new Pagination<ProductDto>
            {
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                Data = data
            };

            return Result<Pagination<ProductDto>>.SuccessResult(
                result,
                data.Any() ? "DataHasBeenReturnedSuccessfully" : "ThereAreNoData");
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(SearchProductParameters dto)
        {
            return await _unitOfWork.Repository<Product>().SearchAsync<SearchProductParameters>(dto);
        }

        public async Task<Result<Pagination<Product>>> SearchAsyncProductsPaged(SearchProductParameters parameters)
        {
            var (data, totalCount) = await _repository.SearchPagedAsync<SearchProductParameters>(
                                    parameters,
                                    page: 1,
                                    pageSize: 10,
                                    includes: new List<Expression<Func<Product, object>>> {
                                        //x => x.Category,
                                        x=>x.Category.Department
                                    });

            var result = new Pagination<Product>
            {
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                Data = data
            };

            return Result<Pagination<Product>>.SuccessResult(
            result,
                data.Any() ? "DataHasBeenReturnedSuccessfully" : "ThereAreNoData");

        }

        public async Task<Result<Pagination<ProductDto>>> SearchAsyncProductsPagedMapped(SearchProductParameters parameters)
        {
            var (data, totalCount) = await _repository.SearchPagedMappedAsync<ProductDto, SearchProductParameters>(
                                    parameters,
                                    page: 1,
                                    pageSize: 10,
                                    includes: new List<Expression<Func<Product, object>>> {
                                        x => x.Category,
                                        x=>x.Category.Department
                                    });

            var result = new Pagination<ProductDto>
            {
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                Data = data
            };

            return Result<Pagination<ProductDto>>.SuccessResult(
            result,
                data.Any() ? "DataHasBeenReturnedSuccessfully" : "ThereAreNoData");

        }



    }
}

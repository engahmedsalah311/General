using Application.DTOs;
using Application.Interfaces;
using Application.Parameters;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;

namespace General.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost("Search")]
        public async Task<IActionResult> Search(SearchProductParameters parameters)
        {
            var result = await _productService.Search(parameters);
            return Ok(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Using generic GetAllAsync method
            var result = await _productService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            // Using generic GetByIdAsync method
            var result = await _productService.GetByIdAsync(id);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductDto productDto)
        {
            // Using generic AddAsync method
            var result = await _productService.AddAsync(productDto);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductDto productDto)
        {
            // Using generic UpdateAsync method
            var result = await _productService.UpdateAsync(id, productDto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // Using generic DeleteAsync method
            var result = await _productService.DeleteAsync(id);
            return Ok(result);
        }

        // Using Custom Service Methods

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable()
        {
            // Using custom method from ProductService
            var result = await _productService.GetAvailableProductsAsync();
            return Ok(result);
        }

        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetByCategory(int category)
        {
            // Using custom method from ProductService
            var result = await _productService.GetProductsByCategoryAsync(category);
            return Ok(result);
        }

        [HttpGet("price-range")]
        public async Task<IActionResult> GetByPriceRange([FromQuery] decimal minPrice, [FromQuery] decimal maxPrice)
        {
            // Using custom method from ProductService
            var result = await _productService.GetProductsInPriceRangeAsync(minPrice, maxPrice);
            return Ok(result);
        }
    }
} 
    

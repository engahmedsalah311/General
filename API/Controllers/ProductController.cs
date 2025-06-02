using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        // Using Generic Service Methods

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _productService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _productService.GetByIdAsync(id);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductDto productDto)
        {
            var result = await _productService.AddAsync(productDto);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductDto productDto)
        {
            var result = await _productService.UpdateAsync(id, productDto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _productService.DeleteAsync(id);
            return Ok(result);
        }

        // Simple paged endpoints
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetPagedItemAsync(
                pageNumber: pageNumber,
                pageSize: pageSize);
            return Ok(result);
        }

        [HttpGet("paged/available")]
        public async Task<IActionResult> GetPagedAvailable(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetPagedItemAsync(
                p => p.IsAvailable,
                pageNumber,
                pageSize);
            return Ok(result);
        }

        // Using Custom Service Methods

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable()
        {
            var result = await _productService.GetAvailableProductsAsync();
            return Ok(result);
        }

        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetByCategory(string category)
        {
            var result = await _productService.GetProductsByCategoryAsync(category);
            return Ok(result);
        }

        [HttpGet("price-range")]
        public async Task<IActionResult> GetByPriceRange([FromQuery] decimal minPrice, [FromQuery] decimal maxPrice)
        {
            var result = await _productService.GetProductsInPriceRangeAsync(minPrice, maxPrice);
            return Ok(result);
        }
    }
} 
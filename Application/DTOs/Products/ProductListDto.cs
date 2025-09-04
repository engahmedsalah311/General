using System.Collections.Generic;

namespace Application.DTOs.Products
{
    public class ProductListDto
    {
        public IEnumerable<ProductDto> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}

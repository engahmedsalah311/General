using Application.DTOs.ProductColours;
using System;

namespace Application.DTOs.Products
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
        public bool IsBestSeller { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<ProductColoursDto>? ProductColours { get; set; }
    }

}

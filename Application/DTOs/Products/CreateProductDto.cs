using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Products
{
    public class CreateProductDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        //[Url]
        public string ImageUrl { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsBestSeller { get; set; } = false;
        
        [Required]
        public int CategoryId { get; set; }

        public List<CreateProductColourDto> Colours { get; set; }
        public int Id { get; set; }
    }

    public class CreateProductColourDto
    {
        public int ColourId { get; set; }
        public int Quantity { get; set; }
    }
}

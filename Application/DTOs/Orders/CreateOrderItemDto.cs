using Application.DTOs.Products;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Orders
{
    public class CreateOrderItemDto
    {
        public CreateOrderItemColourDto? SelectedItems { get; set; }

        //public int Quantity { get; set; } = 1;
        
        public string? Notes { get; set; }
    }
    public class CreateOrderItemColourDto:CreateProductColourDto
    {
        [Required(ErrorMessage = "ProductId is required")]
        public int ProductId { get; set; }
    }
}

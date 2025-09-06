using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Orders
{
    public class AddOrderItemDto
    {
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }
        [Required]
        public int ProductColourId { get; set; }
    }
}

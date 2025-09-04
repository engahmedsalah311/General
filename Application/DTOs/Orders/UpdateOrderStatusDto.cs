using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace Application.DTOs.Orders
{
    public class UpdateOrderStatusDto
    {
        [Required]
        public OrderStatus Status { get; set; }
        
        public string Notes { get; set; }
    }
}

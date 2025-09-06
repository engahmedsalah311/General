using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Orders
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "Shipping address is required")]
        [StringLength(200, ErrorMessage = "Shipping address cannot be longer than 200 characters")]
        public string ShippingAddress { get; set; }

        [StringLength(200, ErrorMessage = "Billing address cannot be longer than 200 characters")]
        public string BillingAddress { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(100, ErrorMessage = "Customer name cannot be longer than 100 characters")]
        public string CustomerName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string CustomerEmail { get; set; }

        [Required(ErrorMessage = "At least one order item is required")]
        public List<CreateOrderItemDto> OrderItems { get; set; } = new();

        [Required(ErrorMessage = "Payment method is required")]
        public PaymentMethod PaymentMethod { get; set; }

        public string Notes { get; set; }

        // For online payments
        public string PaymentToken { get; set; }
    }
}

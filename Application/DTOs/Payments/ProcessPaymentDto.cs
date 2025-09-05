using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Payments
{
    public class ProcessPaymentDto
    {
        [Required]
        public int OrderId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string PaymentMethod { get; set; }
        
        public string PaymentDetails { get; set; }
        public string? Currency { get; set; }
    }
}

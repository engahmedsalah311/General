using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Payments
{
    public class ProcessRefundDto
    {
        [Required]
        public int OrderId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string TransactionId { get; set; }
        
        public string Reason { get; set; }
    }
}

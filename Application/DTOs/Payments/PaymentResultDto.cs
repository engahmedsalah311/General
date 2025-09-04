namespace Application.DTOs.Payments
{
    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TransactionId { get; set; }
    }
}

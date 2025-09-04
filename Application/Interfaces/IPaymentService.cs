using System.Threading.Tasks;
using Application.DTOs.Payments;

namespace Application.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentResultDto> ProcessPaymentAsync(ProcessPaymentDto paymentDto);
        Task<PaymentResultDto> ProcessRefundAsync(ProcessRefundDto refundDto);
        Task<PaymentResultDto> GetPaymentStatusAsync(string transactionId);
    }
}

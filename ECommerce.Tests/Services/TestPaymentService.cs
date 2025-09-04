using System;
using System.Threading.Tasks;
using Application.Interfaces;

namespace ECommerce.Tests.Services
{
    public class TestPaymentService : IPaymentService
    {
        public Task<PaymentResult> ProcessPaymentAsync(string paymentToken, decimal amount, string currency = "EGP")
        {
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                TransactionId = Guid.NewGuid().ToString(),
                Status = "succeeded",
                ErrorMessage = null,
                RawResponse = "Test payment processed successfully"
            });
        }

        public Task<PaymentResult> RefundPaymentAsync(string transactionId, decimal? amount = null)
        {
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                Status = "refunded",
                ErrorMessage = null,
                RawResponse = "Test refund processed successfully"
            });
        }

        public Task<PaymentResult> GetPaymentStatusAsync(string transactionId)
        {
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                Status = "succeeded",
                ErrorMessage = null,
                RawResponse = "Test payment status retrieved successfully"
            });
        }
    }
}

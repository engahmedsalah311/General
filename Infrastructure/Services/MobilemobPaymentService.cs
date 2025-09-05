using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Application.DTOs.Payments;

namespace Infrastructure.Services
{
    public class MobilemobPaymentService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MobilemobPaymentService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public MobilemobPaymentService(
            HttpClient httpClient, 
            IConfiguration configuration,
            ILogger<MobilemobPaymentService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Validate configuration
            _apiKey = configuration["Mobilemob:ApiKey"] ?? 
                throw new InvalidOperationException("Mobilemob:ApiKey is not configured in app settings");
                
            _baseUrl = configuration["Mobilemob:BaseUrl"] ?? "https://api.mobilemob.com/v1";
            
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Mobilemob:ApiKey cannot be empty");
            }
            
            // Configure the HTTP client
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<PaymentResultDto> ProcessPaymentAsync(ProcessPaymentDto paymentDto)
        {
            // Input validation
            if (paymentDto == null)
            {
                throw new ArgumentNullException(nameof(paymentDto));
            }
            
            if (string.IsNullOrWhiteSpace(paymentDto.PaymentDetails))
            {
                throw new ArgumentException("Payment token is required", nameof(paymentDto.PaymentDetails));
            }
            
            if (paymentDto.Amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(paymentDto.Amount), "Amount must be greater than zero");
            }
            
            if (string.IsNullOrWhiteSpace(paymentDto.Currency))
            {
                paymentDto.Currency = "EGP"; // Default to EGP if not specified
            }
            
            _logger.LogInformation("Processing payment of {Amount} {Currency} with token: {Token}", 
                paymentDto.Amount, paymentDto.Currency, MaskSensitiveData(paymentDto.PaymentDetails));
                
            try
            {
                var request = new
                {
                    token = paymentDto.PaymentDetails,
                    amount = (int)(paymentDto.Amount * 100), // Convert to smallest currency unit (e.g., piastres)
                    currency = paymentDto.Currency,
                    description = paymentDto.PaymentDetails ?? "E-commerce purchase"
                };

                var response = await _httpClient.PostAsJsonAsync("payments/charge", request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Mobilemob payment failed: {response.StatusCode} - {content}");
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = $"Payment failed: {response.ReasonPhrase}",
                        TransactionId = null
                    };
                }

                var result = JsonSerializer.Deserialize<MobilemobPaymentResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PaymentResultDto
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Message = result.Success ? "Payment status retrieved successfully" : result.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Mobilemob payment");
                return new PaymentResultDto
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    TransactionId = ""
                };
            }
        }

        public async Task<PaymentResultDto> ProcessRefundAsync(ProcessRefundDto refundDto)
        {
            // Input validation
            if (refundDto == null)
            {
                throw new ArgumentNullException(nameof(refundDto));
            }
            
            if (string.IsNullOrWhiteSpace(refundDto.TransactionId))
            {
                throw new ArgumentException("Transaction ID is required", nameof(refundDto.TransactionId));
            }
            
            if (refundDto.Amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(refundDto.Amount), "Refund amount must be greater than zero");
            }
            
            _logger.LogInformation("Processing refund of {Amount} for transaction {TransactionId}", 
                refundDto.Amount, refundDto.TransactionId);
            
            try
            {
                var request = new
                {
                    amount = (int)(refundDto.Amount * 100) // Convert to smallest currency unit
                };

                var response = await _httpClient.PostAsJsonAsync($"payments/{refundDto.TransactionId}/refund", request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Mobilemob refund failed: {response.StatusCode} - {content}");
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = $"Refund failed: {response.ReasonPhrase}",
                        TransactionId = refundDto.TransactionId
                    };
                }

                var result = JsonSerializer.Deserialize<MobilemobPaymentResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PaymentResultDto
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Message = result.Success ? "Refund processed successfully" : result.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Mobilemob refund for transaction {refundDto.TransactionId}");
                return new PaymentResultDto
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    TransactionId = refundDto.TransactionId
                };
            }
        }

        public async Task<PaymentResultDto> GetPaymentStatusAsync(string transactionId)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new ArgumentException("Transaction ID is required", nameof(transactionId));
            }
            
            _logger.LogDebug("Getting status for transaction {TransactionId}", transactionId);
            
            try
            {
                var response = await _httpClient.GetAsync($"payments/{transactionId}");
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get Mobilemob payment status: {response.StatusCode} - {content}");
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = $"Failed to get payment status: {response.ReasonPhrase}",
                        TransactionId = transactionId
                    };
                }

                var result = JsonSerializer.Deserialize<MobilemobPaymentResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PaymentResultDto
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Message = result.Success ? "Payment status retrieved successfully" : result.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Mobilemob payment status for transaction {transactionId}");
                return new PaymentResultDto
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    TransactionId = transactionId
                };
            }
        }

        // Mobilemob API response model
        private class MobilemobPaymentResponse
        {
            public bool Success { get; set; }
            public string TransactionId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Currency { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
        
        /// <summary>
        /// Masks sensitive data for logging
        /// </summary>
        private static string MaskSensitiveData(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= 4)
            {
                return "****";
            }
            
            return input[..4] + new string('*', input.Length - 4);
        }
    }
}

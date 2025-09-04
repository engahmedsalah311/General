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

        public async Task<PaymentResult> ProcessPaymentAsync(string paymentToken, decimal amount, string currency = "EGP")
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(paymentToken))
            {
                throw new ArgumentException("Payment token is required", nameof(paymentToken));
            }
            
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero");
            }
            
            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentException("Currency is required", nameof(currency));
            }
            
            _logger.LogInformation("Processing payment of {Amount} {Currency} with token: {Token}", 
                amount, currency, MaskSensitiveData(paymentToken));
                
            try
            {
                var request = new
                {
                    token = paymentToken,
                    amount = (int)(amount * 100), // Convert to smallest currency unit (e.g., piastres)
                    currency = currency,
                    description = "E-commerce purchase"
                };

                var response = await _httpClient.PostAsJsonAsync("payments/charge", request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Mobilemob payment failed: {response.StatusCode} - {content}");
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = $"Payment failed: {response.ReasonPhrase}",
                        RawResponse = content
                    };
                }

                var result = JsonSerializer.Deserialize<MobilemobPaymentResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PaymentResult
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Status = result.Status,
                    RawResponse = content
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Mobilemob payment");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    RawResponse = ex.StackTrace
                };
            }
        }

        public async Task<PaymentResult> RefundPaymentAsync(string transactionId, decimal? amount = null)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new ArgumentException("Transaction ID is required", nameof(transactionId));
            }
            
            if (amount.HasValue && amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be greater than zero");
            }
            
            _logger.LogInformation("Processing refund for transaction {TransactionId}", transactionId);
            
            try
            {
                var request = new
                {
                    amount = amount.HasValue ? (int?)(amount.Value * 100) : null
                };

                var response = await _httpClient.PostAsJsonAsync($"payments/{transactionId}/refund", request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Mobilemob refund failed: {response.StatusCode} - {content}");
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = $"Refund failed: {response.ReasonPhrase}",
                        RawResponse = content
                    };
                }

                var result = JsonSerializer.Deserialize<MobilemobPaymentResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PaymentResult
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Status = result.Status,
                    RawResponse = content
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Mobilemob refund for transaction {transactionId}");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    RawResponse = ex.StackTrace
                };
            }
        }

        public async Task<PaymentResult> GetPaymentStatusAsync(string transactionId)
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
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to get payment status: {response.ReasonPhrase}",
                        RawResponse = content
                    };
                }

                var result = JsonSerializer.Deserialize<MobilemobPaymentResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new PaymentResult
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId,
                    Status = result.Status,
                    RawResponse = content
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Mobilemob payment status for transaction {transactionId}");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    RawResponse = ex.StackTrace
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

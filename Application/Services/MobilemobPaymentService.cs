using Application.DTOs.Payments;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using System.Net.Http.Json;
using System.Text.Json;


namespace Application.Services
{
    public class MobilemobPaymentService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MobilemobPaymentService> _logger;
        private readonly IHttpClientFactory _http;
        private readonly PaymobOptions _opt;

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

        public async Task<string> GetAuthTokenAsync()
        {
            var client = _http.CreateClient("paymob");
            var resp = await client.PostAsJsonAsync("api/auth/tokens", new { api_key = _opt.ApiKey });
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            return doc.RootElement.GetProperty("token").GetString()!;
        }

        public async Task<int> CreateOrderAsync(string authToken, long amountCents, string merchantOrderId)
        {
            var client = _http.CreateClient("paymob");
            var payload = new
            {
                auth_token = authToken,
                delivery_needed = "false",
                amount_cents = amountCents,
                currency = "EGP",
                merchant_order_id = merchantOrderId,
                items = new object[] { }
            };
            var resp = await client.PostAsJsonAsync("api/ecommerce/orders", payload);
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            return doc.RootElement.GetProperty("id").GetInt32();
        }
        public async Task<string> GetPaymentKeyAsync(ProcessPaymentDto paymentDto, int orderId)
        {
            var client = _http.CreateClient("paymob");
            var payload = new
            {
                auth_token = paymentDto.Auth_Token,
                amount_cents = paymentDto.Amount_Cents,
                expiration = 3600,
                order_id = orderId,
                billing_data = new
                {
                    email = paymentDto.email,
                    phone_number = paymentDto.phone_number,
                    first_name = paymentDto.first_name,
                    last_name = paymentDto.last_name
                },
                currency = paymentDto.Currency,
                integration_id = _opt.IntegrationId
            };
            var resp = await client.PostAsJsonAsync("api/acceptance/payment_keys", payload);
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            return doc.RootElement.GetProperty("token").GetString()!;
        }
        public string BuildIframeUrl(string paymentToken)
        {
            return $"https://accept.paymob.com/api/acceptance/iframes/{_opt.IframeId}?payment_token={Uri.EscapeDataString(paymentToken)}";
        }



        public async Task<PaymentResultDto> ProcessPaymentAsync(ProcessPaymentDto paymentDto)
        {
            // Input validation
            if (paymentDto == null)
            {
                throw new ArgumentNullException(nameof(paymentDto));
            }
            paymentDto.Auth_Token = await GetAuthTokenAsync();
            
            if (string.IsNullOrWhiteSpace(paymentDto.Auth_Token))
            {
                throw new ArgumentException("Payment token is required", nameof(paymentDto.Auth_Token));
            }
                        
            if (string.IsNullOrWhiteSpace(paymentDto.Currency))
            {
                paymentDto.Currency = "EGP"; // Default to EGP if not specified
            }
            
            _logger.LogInformation("Processing payment of {Amount} {Currency} with token: {Token}", 
                paymentDto.Amount_Cents, paymentDto.Currency, MaskSensitiveData(paymentDto.Auth_Token));
                
            try
            {
                int orderId = await CreateOrderAsync(paymentDto.Auth_Token, paymentDto.Amount_Cents, paymentDto.Merchant_Order_Id);

                if (orderId == 0)
                {
                    _logger.LogError($"Mobilemob payment failed in CreateOrderAcync");
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = $"Mobilemob payment failed in CreateOrderAcync",
                        TransactionId = null
                    };
                }
                else
                {
                    var PaymentKey = await GetPaymentKeyAsync(paymentDto, orderId);
                    if(string.IsNullOrEmpty(PaymentKey))
                    {
                        var IframeUrl = BuildIframeUrl(PaymentKey);
                        return new PaymentResultDto
                        {
                            Success = true,
                            Message = $"First step succeded",
                            TransactionId = PaymentKey
                        };
                    }
                }

                return new PaymentResultDto
                {
                    Success = false,
                    TransactionId = "",
                    Message = "Payment status retrieved failed"
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

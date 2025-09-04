using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ECommerce.Tests.Services
{
    public class MobilemobPaymentServiceTests
    {
        private readonly Mock<ILogger<MobilemobPaymentService>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly MobilemobPaymentService _paymentService;

        public MobilemobPaymentServiceTests()
        {
            _loggerMock = new Mock<ILogger<MobilemobPaymentService>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            
            var inMemorySettings = new Dictionary<string, string> {
                {"Mobilemob:ApiKey", "test-api-key"},
                {"Mobilemob:BaseUrl", "https://api.mobilemob.com/v1"}
            };
            
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();
                
            _paymentService = new MobilemobPaymentService(_httpClient, _configuration, _loggerMock.Object);
        }

        [Fact]
        public async Task ProcessPaymentAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var response = new
            {
                success = true,
                transactionId = "txn_12345",
                status = "succeeded",
                amount = 1000,
                currency = "EGP"
            };

            SetupMockResponse(HttpMethod.Post, "payments/charge", response, HttpStatusCode.OK);

            // Act
            var result = await _paymentService.ProcessPaymentAsync("test_token", 10.0m, "EGP");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("txn_12345", result.TransactionId);
            Assert.Equal("succeeded", result.Status);
        }

        [Fact]
        public async Task ProcessPaymentAsync_InvalidToken_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _paymentService.ProcessPaymentAsync("", 10.0m, "EGP"));
        }

        [Fact]
        public async Task RefundPaymentAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var response = new
            {
                success = true,
                transactionId = "txn_12345",
                status = "refunded",
                amount = 1000,
                currency = "EGP"
            };

            SetupMockResponse(HttpMethod.Post, "payments/txn_12345/refund", response, HttpStatusCode.OK);

            // Act
            var result = await _paymentService.RefundPaymentAsync("txn_12345", 10.0m);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("txn_12345", result.TransactionId);
            Assert.Equal("refunded", result.Status);
        }

        [Fact]
        public async Task GetPaymentStatusAsync_ValidTransactionId_ReturnsStatus()
        {
            // Arrange
            var response = new
            {
                success = true,
                transactionId = "txn_12345",
                status = "succeeded",
                amount = 1000,
                currency = "EGP"
            };

            SetupMockResponse(HttpMethod.Get, "payments/txn_12345", response, HttpStatusCode.OK);

            // Act
            var result = await _paymentService.GetPaymentStatusAsync("txn_12345");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("txn_12345", result.TransactionId);
            Assert.Equal("succeeded", result.Status);
        }

        [Fact]
        public async Task ProcessPaymentAsync_ApiError_ReturnsError()
        {
            // Arrange
            var errorResponse = new
            {
                success = false,
                message = "Invalid API key",
                code = "authentication_error"
            };

            SetupMockResponse(HttpMethod.Post, "payments/charge", errorResponse, HttpStatusCode.Unauthorized);

            // Act
            var result = await _paymentService.ProcessPaymentAsync("test_token", 10.0m, "EGP");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unauthorized", result.ErrorMessage);
        }

        private void SetupMockResponse(HttpMethod method, string url, object response, HttpStatusCode statusCode)
        {
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == method &&
                        req.RequestUri!.PathAndQuery.Contains(url)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
                });
        }
    }
}

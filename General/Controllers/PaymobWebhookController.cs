using Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace General.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymobWebhookController : ControllerBase
    {
        private readonly PaymobOptions _opt;
        public PaymobWebhookController(IOptions<PaymobOptions> opt) { _opt = opt.Value; }

        [HttpPost("webhook")]
        public async Task<IActionResult> Callback()
        {
            Request.EnableBuffering();
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            var bodyBytes = ms.ToArray();
            Request.Body.Position = 0;
            string body = Encoding.UTF8.GetString(bodyBytes);

            // Paymob sends an HMAC signature header — check your dashboard/docs for the exact header name.
            // Common header names: X-Signature, X-Hub-Signature, X-Callback-Signature... update accordingly.
            if (!Request.Headers.TryGetValue("X-Paymob-Signature", out var sigHeader))
            {
                // try alternate header names or read from payload if Paymob puts it there
                // return Unauthorized if cannot locate signature
                return Unauthorized();
            }

            if (!VerifyHmac(sigHeader, bodyBytes, _opt.HmacSecret)) return Unauthorized();

            // Handle payload (deserialize and process status)
            var json = JsonDocument.Parse(body);
            var success = json.RootElement.GetProperty("success").GetBoolean();
            var orderId = json.RootElement.GetProperty("order").GetProperty("merchant_order_id").GetString();
            var amount = json.RootElement.GetProperty("amount_cents").GetString();

            // example: check success flag then update your DB/fulfill order
            return Ok();
        }

        private bool VerifyHmac(string signatureHeader, byte[] payload, string secret)
        {
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            using var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes);
            var hash = hmac.ComputeHash(payload);
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            var b64 = Convert.ToBase64String(hash);

            // Accept common formats (hex, base64, "sha256=hex")
            if (signatureHeader.Equals(hex, StringComparison.OrdinalIgnoreCase)) return true;
            if (signatureHeader.Equals(b64, StringComparison.OrdinalIgnoreCase)) return true;
            if (signatureHeader.Equals("sha256=" + hex, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}

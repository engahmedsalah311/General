using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Payments
{
    public class ProcessPaymentDto
    {
        public string Auth_Token { get; set; }     // auth_token
        public bool Delivery_Needed { get; set; } // "false"
        public long Amount_Cents { get; set; }    // amount_cents
        public string Currency { get; set; }        // "EGP"
        public string Merchant_Order_Id { get; set; } // merchant_order_id
        public string email { get; set; } // merchant_order_id
        public string phone_number { get; set; } // merchant_order_id
        public string first_name { get; set; } // merchant_order_id
        public string last_name { get; set; } // merchant_order_id

    }
}

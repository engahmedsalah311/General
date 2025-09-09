using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application
{
    public class PaymobOptions
    {
        public string ApiKey { get; set; }          // من الـ Dashboard
        public string AuthToken { get; set; }       // هيتولد وقت الـ login
        public int IntegrationId { get; set; }      // لكل طريقة دفع (بطاقة - Wallet - كاش)
        public int IframeId { get; set; }           // Iframe للبطاقة
        public string BaseUrl { get; set; }         // "https://accept.paymob.com"
        public string CallbackUrl { get; set; }     // الرابط اللي Paymob هيبعتله الـ Webhook
        public string  HmacSecret { get; set; } = "";
    }
}

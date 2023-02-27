using Newtonsoft.Json;

namespace Ucommerce.Transactions.Payments.QuickpayLink.Models
{
    internal class CreatePaymentLinkResponse
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}

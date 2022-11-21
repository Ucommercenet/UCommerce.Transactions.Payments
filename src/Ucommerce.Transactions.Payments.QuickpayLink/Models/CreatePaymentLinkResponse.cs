using System.Text.Json.Serialization;

namespace Ucommerce.Transactions.Payments.QuickpayLink.Models
{
    internal class CreatePaymentLinkResponse
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}

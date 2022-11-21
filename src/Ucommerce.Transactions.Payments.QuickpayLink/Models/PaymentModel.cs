using System.Text.Json.Serialization;

namespace Ucommerce.Transactions.Payments.QuickpayLink.Models
{
    internal class PaymentModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("operations")]
        public Operation[] Operations { get; set; }
    }

    internal class Operation
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}

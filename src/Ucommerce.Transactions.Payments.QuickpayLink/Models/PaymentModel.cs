using Newtonsoft.Json;

namespace Ucommerce.Transactions.Payments.QuickpayLink.Models
{
    internal class PaymentModel
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("operations")]
        public Operation[] Operations { get; set; }
    }

    internal class Operation
    {
        [JsonProperty("type")]
        public string Type { get; set; }
    }
}

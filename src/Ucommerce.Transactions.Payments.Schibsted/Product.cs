using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Ucommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class Product
    {
        [DataMember(Name = "clientId")]
        public string ClientId { get; set; }

        [DataMember(Name = "parentProductId")]
        public int? ParentProductId { get; set; }

        [DataMember(Name = "productId")]
        public int? ProductId { get; set; }

        [DataMember(Name = "type")]
        public int? Type { get; set; }

        [DataMember(Name = "bundle")]
        public int? Bundle { get; set; }

        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "price")]
        public int? Price { get; set; }

        [DataMember(Name = "vat")]
        public int? Vat { get; set; }

        [DataMember(Name = "currency")]
        public string Currency { get; set; }

        [DataMember(Name = "subscriptionPeriod")]
        public int? SubscriptionPeriod { get; set; }

        [DataMember(Name = "paymentOptions")]
        public int? PaymentOptions { get; set; }
    }
}

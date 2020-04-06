using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Ucommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class OrderItem
    {
        [DataMember(Name = "clientItemReference")]
        public string ClientItemReference { get; set; }

        [DataMember(Name = "type")]
        public int Type { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "currency")]
        public string Currency { get; set; }

        [DataMember(Name = "price")]
        public int Price { get; set; }

        [DataMember(Name = "vat")]
        public int Vat { get; set; }

        [DataMember(Name = "quantity")]
        public int Quantity { get; set; }

        [DataMember(Name = "productId")]
        public int? ProductId { get; set; }

        public IDictionary<string, object> Properties { get; set; }
    }
}

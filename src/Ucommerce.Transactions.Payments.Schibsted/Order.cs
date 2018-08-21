using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace UCommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class Order
    {
        [DataMember(Name = "clientReference")]
        public string ClientReference { get; set; }

        [DataMember(Name = "status")]
        public int Status { get; set; }

        [DataMember(Name = "items")]
        public List<OrderItem> Items { get; set; }
    }
}
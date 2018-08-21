using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace UCommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class CallbackEntry
    {
        [DataMember(Name = "orderId")]
        public int OrderId { get; set; }

        [DataMember(Name = "changedFields")]
        public string ChangedFields { get; set; }

        [DataMember(Name = "time")]
        public string Time { get; set; }
    }
}
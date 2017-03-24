using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace UCommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class SppContainer<T>
    {
        [DataMember(Name = "object")]
        public string Object { get; set; }

        [DataMember(Name = "data")]
        public T Data { get; set; }
    }
}

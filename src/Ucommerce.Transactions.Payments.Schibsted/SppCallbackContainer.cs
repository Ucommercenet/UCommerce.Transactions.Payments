using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Ucommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class SppCallbackContainer
    {
        [DataMember(Name = "object")]
        public string Object { get; set; }

        [DataMember(Name = "algorithm")]
        public string Algorithm { get; set; }

        [DataMember(Name = "entry")]
        public List<CallbackEntry> Entries { get; set; }
    }
}

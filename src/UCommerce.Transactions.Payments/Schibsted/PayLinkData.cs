using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace UCommerce.Transactions.Payments.Schibsted
{
    [DataContract]
    public class PayLinkData
    {
        [DataMember(Name = "redirectUri")]
        public string RedirectUri { get; set; }

        [DataMember(Name = "shortUrl")]
        public string ShortUrl { get; set; }
    }
}

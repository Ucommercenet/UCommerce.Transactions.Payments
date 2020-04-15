using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ucommerce.Transactions.Payments.Schibsted
{
    public class SchibstedSha256Computer : AbstractMd5Computer
    {
        public string ComputeHash(IEnumerable<string> data, string secret, bool urlEncode)
        {
            var sb = new StringBuilder();
            foreach (var d in data)
                sb.Append(d);

            return ComputeHash(sb.ToString(), secret, urlEncode);
        }

        public string ComputeHash(string data, string secret, bool urlEncode)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            var hashComputer = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hashComputer.ComputeHash(bytes);

            var base64Hash = Convert.ToBase64String(hash);

            if (urlEncode)
                base64Hash = base64Hash.Replace("=", string.Empty).Replace("+", "-").Replace("/", "_");

            return base64Hash;
        }
    }
}

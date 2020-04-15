using System.Security.Cryptography;
using System.Text;

namespace Ucommerce.Transactions.Payments.Ideal
{
    public class IdealSha1Computer
    {

        public IdealSha1Computer()
        {
        }

        public string GetSha1Key(string amount, string purchaseId, string paymentType, string validUntil, string orderId, string description, string secretKey, string merchantId, string subId)
        {
            var concatString = new StringBuilder();
            concatString.Append(secretKey);
            concatString.Append(merchantId);
            concatString.Append(subId);
            concatString.Append(amount);
            concatString.Append(purchaseId);
            concatString.Append(paymentType);
            concatString.Append(validUntil);

            concatString.Append(orderId);
            concatString.Append(description);
            concatString.Append("1");
            concatString.Append(amount);

            concatString = concatString.Replace(" ", "");
            concatString = concatString.Replace("\t", "");
            concatString = concatString.Replace("\n", "");
            concatString = concatString.Replace("&amp;", "&");
            concatString = concatString.Replace("&gt;", ">");
            concatString = concatString.Replace("&lt;", "<");
            concatString = concatString.Replace("&quot;", "\"");

            byte[] data = new ASCIIEncoding().GetBytes(concatString.ToString());
            byte[] hashValue = new SHA1Managed().ComputeHash(data);

            return GetAsHexaDecimal(hashValue);
        }

        /// <summary>
        /// Byte array to Hex Decimal string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public string GetAsHexaDecimal(byte[] bytes)
        {
            var s = new StringBuilder();
            foreach (var b in bytes)
                s.Append(string.Format("{0:x2}", b));
            return s.ToString();
        }
    }
}

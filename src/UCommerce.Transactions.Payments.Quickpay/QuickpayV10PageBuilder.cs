using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ucommerce.Extensions;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Quickpay
{
    public class QuickpayV10PageBuilder : QuickpayPageBuilder
    {
        public QuickpayV10PageBuilder(QuickpayMd5Computer md5Computer, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl) : base(md5Computer, absoluteUrlService, callbackUrl)
        {
        }

        protected override string PROTOCOL => "v10";

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append(@"<form id=""Quickpay"" name=""Quickpay"" method=""post"" action=""https://payment.quickpay.net"">");

            var parameters = GetParameters(paymentRequest);
            AddParameters(page, parameters);

            page.Append("</form>");
        }
        
        protected override IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
        {
            var result = base.GetParameters(paymentRequest);

            string apiKey = paymentRequest.PaymentMethod.DynamicProperty<string>().ApiKey;

            result["version"] = PROTOCOL;
            result["merchant_id"] = result["merchant"];
            result["agreement_id"] = paymentRequest.PaymentMethod.DynamicProperty<string>().AgreementId;
            result["order_id"] = paymentRequest.Payment.ReferenceId;

            result.Remove("md5check");
            result.Remove("testmode");
            result.Remove("merchant");
            result.Remove("msgtype");
            result.Remove("ordernumber");
            result.Remove("protocol");

            result["checksum"] = Sign(result, apiKey);

            return result;
        }

        private string Sign(IDictionary<string, string> parameters, string apiKey)
        {
            var result = String.Join(" ", parameters.OrderBy(c => c.Key).Select(c => c.Value).ToArray());
            var e = Encoding.UTF8;
            var hmac = new HMACSHA256(e.GetBytes(apiKey));
            byte[] b = hmac.ComputeHash(e.GetBytes(result));
            var s = new StringBuilder();
            for (int i = 0; i < b.Length; i++)
            {
                s.Append(b[i].ToString("x2"));
            }
            return s.ToString();
        }
    }
}

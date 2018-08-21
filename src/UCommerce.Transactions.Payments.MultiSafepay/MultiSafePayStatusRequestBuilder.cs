using System.Text;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;

namespace UCommerce.Transactions.Payments.MultiSafepay
{
    /// <summary>
    /// Builds the Xml document needed for the status request and returns it as a string.
    /// </summary>
    public class MultiSafepayStatusRequestBuilder
    {
        public string BuildRequest(string transactionId, PaymentMethod paymentMethod)
        {
	        string accountId = paymentMethod.DynamicProperty<string>().AccountId;
	        string siteId = paymentMethod.DynamicProperty<string>().SiteId;
	        string siteSecurityCode = paymentMethod.DynamicProperty<string>().SiteSecurityCode;

            var requestBuilder = new StringBuilder();

            requestBuilder.AppendFormat("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            requestBuilder.AppendFormat("<status>");
            requestBuilder.AppendFormat("  <merchant>");
            requestBuilder.AppendFormat("     <account>{0}</account>", accountId);
            requestBuilder.AppendFormat("     <site_id>{0}</site_id>", siteId);
            requestBuilder.AppendFormat("     <site_secure_code>{0}</site_secure_code>", siteSecurityCode);
            requestBuilder.AppendFormat("  </merchant>");
            requestBuilder.AppendFormat("  <transaction>");
            requestBuilder.AppendFormat("     <id>{0}</id>", transactionId);
            requestBuilder.AppendFormat("  </transaction>");
            requestBuilder.AppendFormat("</status>");

            return requestBuilder.ToString();
        }
    }
}

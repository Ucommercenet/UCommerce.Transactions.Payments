using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.MultiSafepay
{
    /// <summary>
    /// Builds the XML document needed for the payment url request and returns it as a string.
    /// </summary>
    public class MultiSafepayPaymentRequestBuilder
    {
	    private readonly ICallbackUrl _callbackUrl;
	    private readonly IAbsoluteUrlService _absoluteUrlService;

	    public MultiSafepayPaymentRequestBuilder(ICallbackUrl callbackUrl, IAbsoluteUrlService absoluteUrlService)
	    {
		    _callbackUrl = callbackUrl;
		    _absoluteUrlService = absoluteUrlService;
	    }

	    public string BuildRequest(PaymentRequest paymentRequest)
    	{
    		string accountId = paymentRequest.PaymentMethod.DynamicProperty<string>().AccountId;
    		string siteId = paymentRequest.PaymentMethod.DynamicProperty<string>().SiteId;
    		string siteSecurityCode = paymentRequest.PaymentMethod.DynamicProperty<string>().SiteSecurityCode;
			string callbackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string acceptUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string cancelUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CancelUrl;

            var requestBuilder = new StringBuilder();
            OrderAddress billingAddress = paymentRequest.PurchaseOrder.GetBillingAddress();

            var md5Hasher = new MD5CryptoServiceProvider();
            var encoder = new UTF8Encoding();

            string ipAddress = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
            string forwardedIp = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            byte[] hashedBytes = md5Hasher.ComputeHash(encoder.GetBytes(paymentRequest.Amount.Value.ToCents() + paymentRequest.Amount.CurrencyIsoCode + accountId + siteId + paymentRequest.Payment.ReferenceId));
            string signature = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            
            requestBuilder.AppendFormat(@"<?xml version='1.0' encoding='utf-8'?>");
            requestBuilder.AppendFormat(@"<redirecttransaction ua='custom-1.2'>");
            requestBuilder.AppendFormat(@"  <merchant> ");
            requestBuilder.AppendFormat(@"    <account>{0}</account>", accountId);
            requestBuilder.AppendFormat(@"    <site_id>{0}</site_id>", siteId);
            requestBuilder.AppendFormat(@"    <site_secure_code>{0}</site_secure_code>", siteSecurityCode);
            requestBuilder.AppendFormat(@"    <notification_url>{0}</notification_url>", _callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment));
            requestBuilder.AppendFormat(@"    <redirect_url>{0}</redirect_url>", 
				new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder));
            requestBuilder.AppendFormat(@"    <cancel_url>{0}</cancel_url>", 
				new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder));
            requestBuilder.AppendFormat(@"  </merchant>");
            requestBuilder.AppendFormat(@"  <customer>");
            requestBuilder.AppendFormat(@"    <locale>{0}</locale>", paymentRequest.PurchaseOrder.CultureCode);
            requestBuilder.AppendFormat(@"    <ipaddress>{0}</ipaddress>", ipAddress);
            requestBuilder.AppendFormat(@"    <forwardedip>{0}</forwardedip>", forwardedIp);
            requestBuilder.AppendFormat(@"    <firstname>{0}</firstname>", XmlHelper.ConvertToValidXmlText(billingAddress.FirstName));
            requestBuilder.AppendFormat(@"    <lastname>{0}</lastname>", XmlHelper.ConvertToValidXmlText(billingAddress.LastName)); 
            requestBuilder.AppendFormat(@"    <address1>{0}</address1>", XmlHelper.ConvertToValidXmlText(billingAddress.Line1));
            requestBuilder.AppendFormat(@"    <address2>{0}</address2>", XmlHelper.ConvertToValidXmlText(billingAddress.Line2));
            requestBuilder.AppendFormat(@"    <zipcode>{0}</zipcode>", XmlHelper.ConvertToValidXmlText(billingAddress.PostalCode));
            requestBuilder.AppendFormat(@"    <city>{0}</city>", XmlHelper.ConvertToValidXmlText(billingAddress.City));
            requestBuilder.AppendFormat(@"    <state>{0}</state>", XmlHelper.ConvertToValidXmlText(billingAddress.State));
            requestBuilder.AppendFormat(@"    <phone>{0}</phone>", XmlHelper.ConvertToValidXmlText(billingAddress.PhoneNumber));
            requestBuilder.AppendFormat(@"    <email>{0}</email>", XmlHelper.ConvertToValidXmlText(billingAddress.EmailAddress));
            requestBuilder.AppendFormat(@"  </customer>");
            requestBuilder.AppendFormat(@"  <transaction>");
            requestBuilder.AppendFormat(@"    <id>{0}</id>", paymentRequest.Payment.ReferenceId);
            requestBuilder.AppendFormat(@"    <currency>{0}</currency>", paymentRequest.Amount.CurrencyIsoCode);
            requestBuilder.AppendFormat(@"    <amount>{0}</amount>", paymentRequest.Amount.Value.ToCents());
            requestBuilder.AppendFormat(@"    <description>{0}</description>", paymentRequest.Payment.ReferenceId);

            var itemStringBuilder = new StringBuilder();
            foreach (var orderLine in paymentRequest.PurchaseOrder.OrderLines)
            {
                itemStringBuilder.AppendFormat(@"<li>{0} x {1}, {2}</li>", orderLine.Quantity, XmlHelper.ConvertToValidXmlText(orderLine.ProductName), orderLine.Total);
            }

            requestBuilder.AppendFormat(@"    <items><ul>{0}</ul></items>", itemStringBuilder);
            requestBuilder.AppendFormat(@"  </transaction>");
            requestBuilder.AppendFormat(@"  <signature>{0}</signature>", signature);
            requestBuilder.AppendFormat(@"</redirecttransaction>");

            return requestBuilder.ToString();
        }
    }
}

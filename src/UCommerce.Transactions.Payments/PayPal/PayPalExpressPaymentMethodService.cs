using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.Configuration;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.PayPal
{
	/// <summary>
	/// PayPal Express payment provider.
	/// </summary>
	/// <remarks>
	/// More info at https://developer.paypal.com/webapps/developer/docs/classic/express-checkout/integration-guide/ECGettingStarted/.
	/// </remarks>
	public class PayPalExpressPaymentMethodService : PayPalPaymentMethodService
	{
		private const string URL_SANDBOX = "https://api-3t.sandbox.paypal.com/nvp";
		private const string URL_PRODUCTION = "https://api-3t.paypal.com/nvp";

		public string GetPostUrl(PaymentMethod paymentMethod)
		{
			return paymentMethod.DynamicProperty<bool>().Sandbox ? URL_SANDBOX : URL_PRODUCTION;
		}

		private const string WINDOW_URLTEMPLATE_SANDBOX = "https://www.sandbox.paypal.com/webscr?cmd=_express-checkout&token={0}&useraction=commit";
		private const string WINDOW_URLTEMPLATE_PRODUCTION = "https://www.paypal.com/webscr?cmd=_express-checkout&token={0}";

		public string GetWindowUrlTemplate(PaymentMethod paymentMethod)
		{
			return paymentMethod.DynamicProperty<bool>().Sandbox ? WINDOW_URLTEMPLATE_SANDBOX : WINDOW_URLTEMPLATE_PRODUCTION;
		}

		private readonly IOrderService _orderService;
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private readonly ICallbackUrl _callbackUrl;
		protected bool Debug { get; set; }

		public PayPalExpressPaymentMethodService(
			IOrderService orderService,
			ILoggingService loggingService,
			IAbsoluteUrlService absoluteUrlService,
			ICallbackUrl callbackUrl)
			: base(null, loggingService)
		{
			_orderService = orderService;
			_absoluteUrlService = absoluteUrlService;
			_callbackUrl = callbackUrl;
		}

		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			Payment payment = paymentRequest.Payment;

			var values = GetParameters(paymentRequest);

			LogAuditTrail(payment.PurchaseOrder, "SetExpressCheckout", values);

			var post = new HttpPost(GetPostUrl(paymentRequest.PaymentMethod), values);
			string response = post.GetString();
			NameValueCollection responseValues = HttpUtility.ParseQueryString(response);

			LogAuditTrail(payment.PurchaseOrder, "SetExpressCheckoutResponse", responseValues);

			if (responseValues["ACK"] != "Success")
			{
				throw new Exception(response);
			}

			string windowUrl = string.Format(GetWindowUrlTemplate(paymentRequest.PaymentMethod), responseValues["TOKEN"]);
			HttpContext.Current.Response.Redirect(windowUrl, true);

			return payment;
		}

		public virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			Payment payment = paymentRequest.Payment;

			OrderAddress billingAddress = payment.PurchaseOrder.GetBillingAddress();

			var orderAddress = billingAddress;
			var shipment = payment.PurchaseOrder.Shipments.FirstOrDefault();
			if (shipment != null && shipment.ShipmentAddress != null)
				orderAddress = shipment.ShipmentAddress;


			string orderCurrency = paymentRequest.Payment.PurchaseOrder.BillingCurrency.ISOCode;
			string referenceId = payment.ReferenceId;


			string callbackUrl = _callbackUrl.GetCallbackUrl(paymentRequest.PaymentMethod.DynamicProperty<string>().NotifyUrl, payment);
			string cancelUrl = _absoluteUrlService.GetAbsoluteUrl(paymentRequest.PaymentMethod.DynamicProperty<string>().CancelReturn);

			IDictionary<string, string> values = new Dictionary<string, string>();

			// Set up call
			values.Add("USER", paymentRequest.PaymentMethod.DynamicProperty<string>().ApiUsername);
			values.Add("PWD", paymentRequest.PaymentMethod.DynamicProperty<string>().ApiPassword);
			values.Add("SIGNATURE", paymentRequest.PaymentMethod.DynamicProperty<string>().ApiSignature);
			values.Add("METHOD", "SetExpressCheckout");
			values.Add("VERSION", "98.0");

			//Add order values
			values.Add("RETURNURL", callbackUrl);
			values.Add("CANCELURL", cancelUrl);
			values.Add("NOSHIPPING", "1");
			values.Add("ALLOWNOTE", "0");
			var regionCode = GetRegionCode(paymentRequest);
			values.Add("LOCALECODE", regionCode);
			values.Add("SOLUTIONTYPE", "Sole");
			values.Add("EMAIL", billingAddress.EmailAddress);
            
            //Vendor code to identify uCommerce
			values.Add("BUTTONSOURCE", "uCommerce_SP");

			values.Add("PAYMENTREQUEST_0_SHIPTONAME",
				Uri.EscapeDataString(string.Format("{0} {1}", orderAddress.FirstName, orderAddress.LastName)));
			values.Add("PAYMENTREQUEST_0_SHIPTOSTREET", Uri.EscapeDataString(orderAddress.Line1));
			values.Add("PAYMENTREQUEST_0_SHIPTOSTREET2", Uri.EscapeDataString(orderAddress.Line2));
			values.Add("PAYMENTREQUEST_0_SHIPTOCITY", Uri.EscapeDataString(orderAddress.City));
			values.Add("PAYMENTREQUEST_0_SHIPTOSTATE", Uri.EscapeDataString(orderAddress.State));
			values.Add("PAYMENTREQUEST_0_SHIPTOZIP", Uri.EscapeDataString(orderAddress.PostalCode));
			values.Add("PAYMENTREQUEST_0_SHIPTOCOUNTRYCODE", Uri.EscapeDataString(orderAddress.Country.TwoLetterISORegionName));
			values.Add("PAYMENTREQUEST_0_SHIPTOPHONENUM", Uri.EscapeDataString(orderAddress.PhoneNumber));

			string fullAmount = NumberWithTwoDecimalDigitsAfterPeriodAndNoThousandsSeperator(payment.Amount);
			values.Add("PAYMENTREQUEST_0_AMT", fullAmount);
			values.Add("PAYMENTREQUEST_0_CURRENCYCODE", orderCurrency);

			PaymentAction pa =
				EnumExtensions.ParsePaymentActionThrowExceptionOnFailure(payment.PaymentMethod.DynamicProperty<string>().PaymentAction);
			values.Add("PAYMENTREQUEST_0_PAYMENTACTION", pa.ToString());

			values.Add("PAYMENTREQUEST_0_INVNUM", referenceId);
			return values;
		}

		public virtual string GetRegionCode(PaymentRequest paymentRequest)
		{
			if (paymentRequest.PurchaseOrder.BillingAddress != null && !string.IsNullOrWhiteSpace(paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName))
			{
				return paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName;
			}

			try
			{
				var region = new RegionInfo(paymentRequest.PurchaseOrder.CultureCode ?? "en-us");
				var regionCode = region.TwoLetterISORegionName;

				return regionCode;
			}
			catch (ArgumentException)
			{
				// TODO: Is this the proper default value?
				return "US";
			}
		}

		private void LogAuditTrail(PurchaseOrder purchaseOrder, string title, IEnumerable<KeyValuePair<string, string>> values)
		{
			var nvc = new NameValueCollection();
			foreach (var kvp in values)
			{
				nvc.Add(kvp.Key, kvp.Value);
			}
			LogAuditTrail(purchaseOrder, title, nvc);
		}

		private void LogAuditTrail(PurchaseOrder purchaseOrder, string title, NameValueCollection values)
		{
			var msgId = "L" + Guid.NewGuid().ToString("N");
			var sb = new StringBuilder();

			sb.AppendFormat("{0}: <a href=\"javascript:$('#{1}').toggle(); return false;\">Vis</a><br/>", title, msgId);
			sb.AppendLine();
			sb.AppendFormat("<span id=\"{0}\" style=\"display: none;\">", msgId);
			foreach (string key in values)
			{
				foreach (string value in values.GetValues(key))
				{
					sb.AppendFormat("{0}: {1}<br/>", key, value);
					sb.AppendLine();
				}
			}
			sb.Append("</span>");
			_orderService.AddAuditTrail(purchaseOrder, sb.ToString());
			purchaseOrder.Save();
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotImplementedException();
		}

		public override void ProcessCallback(Payment payment)
		{
			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
				return;

			string token = HttpContext.Current.Request.QueryString["token"];
			var responseValues = EnsureTransactionIsValid(payment, token);
			string transactionId = DoPayment(payment, responseValues);

			if (InstantAcquireIsConfigured(payment))
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
			else
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
			
			payment.TransactionId = transactionId;

			ExecutePostProcessingPipeline(payment);

			string returnUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(payment.PaymentMethod.DynamicProperty<string>().Return))
				.AddOrderGuidParameter(payment.PurchaseOrder)
				.ToString();

			HttpContext.Current.Response.Redirect(returnUrl, true);
		}

		private bool InstantAcquireIsConfigured(Payment payment)
		{
			return payment.PaymentMethod.DynamicProperty<string>().PaymentAction == "Sale";
		}

		protected NameValueCollection EnsureTransactionIsValid(Payment payment, string token)
		{
			IDictionary<string, string> values = new Dictionary<string, string>();
			// Set up call
			values.Add("USER", payment.PaymentMethod.DynamicProperty<string>().ApiUsername);
			values.Add("PWD", payment.PaymentMethod.DynamicProperty<string>().ApiPassword);
			values.Add("SIGNATURE", payment.PaymentMethod.DynamicProperty<string>().ApiSignature);
			values.Add("METHOD", "GetExpressCheckoutDetails");
			values.Add("VERSION", "98.0");
			values.Add("TOKEN", token);

			LogAuditTrail(payment.PurchaseOrder, "GetExpressCheckoutDetails", values);

			var post = new HttpPost(GetPostUrl(payment.PaymentMethod), values);
			string response = post.GetString();

			NameValueCollection responseValues = HttpUtility.ParseQueryString(response);
			LogAuditTrail(payment.PurchaseOrder, "GetExpressCheckoutDetailsResponse", responseValues);
			if (responseValues["ACK"] != "Success")
			{
				throw new Exception(response);
			}

			var responseAmount = responseValues["PAYMENTREQUEST_0_AMT"];
			CheckReceivedAmountAgainstExpectedValue(payment.Amount, responseAmount, response);

			string responseCurrency = responseValues["PAYMENTREQUEST_0_CURRENCYCODE"];
			if (payment.PurchaseOrder.BillingCurrency.ISOCode != responseCurrency)
			{
				string message = string.Format("Wrong currency! Expected {0}, but was {1}", payment.PurchaseOrder.BillingCurrency.ISOCode, responseCurrency);
				throw new Exception(message + " - " + response);
			}
			return responseValues;
		}

		protected string DoPayment(Payment payment, NameValueCollection details)
		{
			IDictionary<string, string> values = new Dictionary<string, string>();
			// Set up call
			values.Add("USER", payment.PaymentMethod.DynamicProperty<string>().ApiUsername);
			values.Add("PWD", payment.PaymentMethod.DynamicProperty<string>().ApiPassword);
			values.Add("SIGNATURE", payment.PaymentMethod.DynamicProperty<string>().ApiSignature);
			values.Add("METHOD", "DoExpressCheckoutPayment");
			values.Add("VERSION", "98.0");

			string[] fieldsToCopy = { "TOKEN", "PAYERID", "PAYMENTREQUEST_0_CURRENCYCODE", "PAYMENTREQUEST_0_AMT" };
			foreach (string key in details)
			{
				if (fieldsToCopy.Contains(key))
				{
					values.Add(key, details[key]);
				}
			}

			PaymentAction pa = EnumExtensions.ParsePaymentActionThrowExceptionOnFailure(payment.PaymentMethod.DynamicProperty<string>().PaymentAction);
			values.Add("PAYMENTREQUEST_0_PAYMENTACTION", pa.ToString());

			LogAuditTrail(payment.PurchaseOrder, "DoExpressCheckoutPayment", values);
			var post = new HttpPost(GetPostUrl(payment.PaymentMethod), values);
			string response = post.GetString();

			NameValueCollection responseValues = HttpUtility.ParseQueryString(response);
			LogAuditTrail(payment.PurchaseOrder, "DoExpressCheckoutPaymentResponse", responseValues);
			if (responseValues["ACK"] != "Success")
			{
				throw new Exception(response);
			}

			var responseAmount = responseValues["PAYMENTINFO_0_AMT"];

			CheckReceivedAmountAgainstExpectedValue(payment.Amount, responseAmount, response);

			string responseCurrency = responseValues["PAYMENTINFO_0_CURRENCYCODE"];
			if (payment.PurchaseOrder.BillingCurrency.ISOCode != responseCurrency)
			{
				string message = string.Format("Wrong currency. Expected {0}, but was {1}", payment.PurchaseOrder.BillingCurrency.ISOCode, responseCurrency);
				throw new Exception(message + " - " + response);
			}

			return responseValues["PAYMENTINFO_0_TRANSACTIONID"];
		}

		protected virtual void CheckReceivedAmountAgainstExpectedValue(decimal expected, string paypalStringRepresentation, string response)
		{
			string orderAmount = NumberWithTwoDecimalDigitsAfterPeriodAndNoThousandsSeperator(expected);

			paypalStringRepresentation = ConvertPayPalAmountStringToExpectedStringRepresentation(paypalStringRepresentation);

			if (orderAmount != paypalStringRepresentation)
			{
				string message = string.Format("Wrong amount. Expected {0}, but was {1}", orderAmount, paypalStringRepresentation);
				throw new Exception(message + " - " + response);
			}
		}

		protected virtual string ConvertPayPalAmountStringToExpectedStringRepresentation(string s)
		{
			// PayPal may or may not return values with a comma as thousands seperator. We need to remove these, to make a stable comparison with the expected amount.
			s = s.Replace(",", string.Empty);

			if (!s.Contains("."))
			{
				// The amount might not contain a decimal point. If not, we need to add it.
				s += ".00";
			}
			return s;
		}

		protected virtual string NumberWithTwoDecimalDigitsAfterPeriodAndNoThousandsSeperator(decimal price)
		{
			return price.ToString("0.00", CultureInfo.InvariantCulture);
		}
	}
}
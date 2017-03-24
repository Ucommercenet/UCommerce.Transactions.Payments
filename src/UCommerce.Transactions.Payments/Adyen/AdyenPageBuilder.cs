using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Infrastructure.Globalization;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.Configuration;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.Adyen
{
	public class AdyenPageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;
		private CustomGlobalization LocalizationContext { get; set; }

		private readonly string[] _fieldsInSignature =
			{
				"paymentAmount",
				"currencyCode",
				"shipBeforeDate",
				"merchantReference",
				"skinCode",
				"merchantAccount",
				"sessionValidity",
				"shopperEmail",
				"shopperReference",
				"recurringContract",
				"allowedMethods",
				"blockedMethods",
				"shopperStatement",
				"merchantReturnData",
				"billingAddressType",
				"deliveryAddressType",
				"offset"
			};

		private const string MerchantSigFieldName = "merchantSig";

		public string PostUrl(PaymentMethod paymentMethod)
		{
			// For dynamics, we have to call the extension method as a normal method.
			AdyenPaymentFlowSelection flowSelection = EnumExtensions.ParseFlowSelectionThrowExceptionOnFailure(paymentMethod.DynamicProperty<string>().FlowSelection);

			if (paymentMethod.DynamicProperty<bool>().Live)
			{

				switch (flowSelection)
				{
					case AdyenPaymentFlowSelection.OnePage:
						return "https://live.adyen.com/hpp/pay.shtml";
					case AdyenPaymentFlowSelection.MultiplePage:
						return "https://live.adyen.com/hpp/select.shtml";
					case AdyenPaymentFlowSelection.DirectoryLookup:
						return "https://live.adyen.com/hpp/directory.shtml";
					case AdyenPaymentFlowSelection.SkipSelect:
						return "https://live.adyen.com/hpp/details.shtml";
					default:
						throw new InvalidOperationException("Invalid FlowSelection");
				}
			}
			switch (flowSelection)
			{
				case AdyenPaymentFlowSelection.OnePage:
					return "https://test.adyen.com/hpp/pay.shtml";
				case AdyenPaymentFlowSelection.MultiplePage:
					return "https://test.adyen.com/hpp/select.shtml";
				case AdyenPaymentFlowSelection.DirectoryLookup:
					return "https://test.adyen.com/hpp/directory.shtml";
				case AdyenPaymentFlowSelection.SkipSelect:
					return "https://test.adyen.com/hpp/details.shtml";
				default:
					throw new InvalidOperationException("Invalid FlowSelection");
			}
		}

		public AdyenPageBuilder(CommerceConfigurationProvider configProvider, ICallbackUrl callbackUrl)
		{
			_callbackUrl = callbackUrl;
			LocalizationContext = new CustomGlobalization(configProvider);
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>Adyen</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if (!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append(@"<form method=""post"" action=""" + PostUrl(paymentRequest.PaymentMethod) + @""">");

			// All parameter fields
			IDictionary<string, string> dict = GetParameters(paymentRequest);
			dict.Add("merchantSig", CalculateMerchantSignature(dict, paymentRequest.PaymentMethod));

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");

			foreach (var pair in dict)
			{
				AddHiddenField(page, pair.Key, pair.Value);
			}

			page.Append("</form>");
		}

		public virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			var dict = new Dictionary<string, string>();

			if (paymentRequest.PaymentMethod.DynamicProperty<bool>().UseRecurringContract)
			{
				// Setup a recurring contract at Adyen.
				dict.Add("recurringContract", "RECURRING");
				dict.Add("paymentAmount", "100");

				if (string.IsNullOrEmpty(paymentRequest.PurchaseOrder.BillingAddress.EmailAddress))
				{
					throw new InvalidOperationException("Customer must have an email, when using a recurring contract.");
				}
			}
			else
			{
				dict.Add("paymentAmount", paymentRequest.Amount.Value.ToCents().ToString(CultureInfo.InvariantCulture));
			}

			dict.Add("merchantReference", paymentRequest.Payment.ReferenceId);
			dict.Add("currencyCode", paymentRequest.Amount.Currency.ISOCode);
			dict.Add("shipBeforeDate", BuildFutureTimestamp(paymentRequest.PaymentMethod.DynamicProperty<int>().ShipBeforeDatePlusDays, paymentRequest.PaymentMethod.DynamicProperty<int>().ShipBeforeDatePlusHours, paymentRequest.PaymentMethod.DynamicProperty<int>().ShipBeforeDatePlusMinutes));
			dict.Add("skinCode", paymentRequest.PaymentMethod.DynamicProperty<string>().SkinCode);
			dict.Add("merchantAccount", paymentRequest.PaymentMethod.DynamicProperty<string>().MerchantAccount);
			dict.Add("shopperLocale", LocalizationContext.CurrentCultureCode.Replace('-', '_'));
			// orderData (optional)
			dict.Add("sessionValidity", BuildFutureTimestamp(0, 0, paymentRequest.PaymentMethod.DynamicProperty<int>().SessionValidityPlusMinutes));
			dict.Add("merchantReturnData", paymentRequest.Payment.ReferenceId);
			if (!string.IsNullOrEmpty(paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName))
				dict.Add("countryCode", paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName);
			if (!string.IsNullOrEmpty(paymentRequest.PurchaseOrder.BillingAddress.EmailAddress))
			{
				dict.Add("shopperEmail", paymentRequest.PurchaseOrder.BillingAddress.EmailAddress);
				dict.Add("shopperReference", paymentRequest.Payment.ReferenceId);
			}
			dict.Add("allowedMethods", paymentRequest.PaymentMethod.DynamicProperty<string>().AllowedMethods);
			dict.Add("blockedMethods", paymentRequest.PaymentMethod.DynamicProperty<string>().BlockedMethods);
			dict.Add("offset", paymentRequest.PaymentMethod.DynamicProperty<string>().Offset.ToString(CultureInfo.InvariantCulture)); // Per user value?
			// shopperStatement (optional) // Per user value?
			if (paymentRequest.PaymentMethod.DynamicProperty<bool>().OfferEmail)
				dict.Add("offerEmail", "prompt");

			// Brand code is used with the DirectoryLookup page flow selection.
			if (!string.IsNullOrEmpty(paymentRequest.PaymentMethod.DynamicProperty<string>().BrandCode))
				dict.Add("brandCode", paymentRequest.PaymentMethod.DynamicProperty<string>().BrandCode);

			if (!string.IsNullOrEmpty(paymentRequest.PaymentMethod.DynamicProperty<string>().ResultUrl))
				dict.Add("resURL", _callbackUrl.GetCallbackUrl(paymentRequest.PaymentMethod.DynamicProperty<string>().ResultUrl, paymentRequest.Payment));

			return dict;
		}

		protected string CalculateMerchantSignature(IDictionary<string, string> dict, PaymentMethod paymentMethod)
		{
			string signature;

			if (paymentMethod.DynamicProperty<string>().SigningAlgorithm == "SHA256")
			{
				var signingString = BuildSigningStringForSHA256(dict);
				var calculator = new HmacCalculatorSHA256(HttpUtility.UrlDecode(paymentMethod.DynamicProperty<string>().HmacSharedSecret));
				signature = calculator.Execute(signingString);
			}
			else
			{
				var signingString = BuildSigningString(dict);
				var calculator = new HmacCalculator(HttpUtility.UrlDecode(paymentMethod.DynamicProperty<string>().HmacSharedSecret));
				signature = calculator.Execute(signingString);
			}


			return signature;
		}

		private string BuildSigningString(IDictionary<string, string> dict)
		{
			return _fieldsInSignature.Where(dict.ContainsKey).Aggregate(string.Empty, (current, fieldName) => current + dict[fieldName]);
		}

		private string BuildSigningStringForSHA256(IDictionary<string, string> dict)
		{
			Dictionary<string, string> signDict = dict.OrderBy(d => d.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
			string keystring = string.Join(":", signDict.Keys);
			string valuestring = string.Join(":", signDict.Values.Select(EscapeValue));
			return string.Format("{0}:{1}", keystring, valuestring);
		}

		private string EscapeValue(string value)
		{
			if (value == null)
			{
				return string.Empty;
			}

			value = value.Replace(@"\", @"\\");
			value = value.Replace(":", @"\:");
			return value;
		}

		private string BuildFutureTimestamp(int daysIntoTheFuture, int hoursIntoTheFuture, int minutesIntoTheFuture)
		{
			return DateTime.Now
				.AddDays(daysIntoTheFuture)
				.AddHours(hoursIntoTheFuture)
				.AddMinutes(minutesIntoTheFuture)
				.ToUniversalTime().ToString("s", DateTimeFormatInfo.InvariantInfo) + "Z";
		}
	}
}

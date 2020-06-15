using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure.Globalization;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.EPay
{
	/// <summary>
	/// Builds a EPay redirect page.
	/// </summary>
	public class EPayPageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private CustomGlobalization LocalizationContext { get; set; }
		private EPayMd5Computer Md5Computer { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="EPayPageBuilder"/> class.
		/// </summary>
		public EPayPageBuilder(EPayMd5Computer md5Computer, ICallbackUrl callbackUrl, IAbsoluteUrlService absoluteUrlService)
		{
			_callbackUrl = callbackUrl;
			_absoluteUrlService = absoluteUrlService;
			LocalizationContext = new CustomGlobalization();
			Md5Computer = md5Computer;
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>EPay</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if(!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		/// <summary>
		/// Get the language.
		/// </summary>
		/// <param name="paymentRequest"></param>
		protected virtual string GetLanguageCode(PaymentRequest paymentRequest)
		{
			var culture = paymentRequest.Payment != null && paymentRequest.Payment.PurchaseOrder != null &&
						  paymentRequest.PurchaseOrder.CultureCode != null
							  ? new CultureInfo(paymentRequest.Payment.PurchaseOrder.CultureCode)
							  : new CultureInfo("en-us");

			// Culture based on order.
			// It's used in the AddLanguage call.
			LocalizationContext.SetCulture(culture);
			var languageCode = "2";
			switch (LocalizationContext.CurrentCultureCode)
			{
				case "da":
				case "da-DK":
					languageCode = "1";
					break;
				case "sv":
				case "sv-SE":
					languageCode = "3";
					break;
				case "no":
				case "nb-NO":
				case "nn-NO":
					languageCode = "4";
					break;
				case "is":
				case "is-IS":
					languageCode = "6";
					break;
				case "de":
				case "de-DE":
					languageCode = "7";
					break;
				case "fi":
				case "fi-FI":
					languageCode = "8";
					break;
			}

			return languageCode;
		}

		/// <summary>
		/// Gets the parameters. Override this method if you want to add other parameters.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns></returns>
		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			string merchantNumber = paymentRequest.PaymentMethod.DynamicProperty<string>().MerchantNumber;
			string acceptUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string declineUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().DeclineUrl;
			string callbackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			bool ownReceipt = paymentRequest.PaymentMethod.DynamicProperty<bool>().OwnReceipt;
			bool instantAcquire = paymentRequest.PaymentMethod.DynamicProperty<bool>().InstantAcquire;
			bool splitPayment = paymentRequest.PaymentMethod.DynamicProperty<bool>().SplitPayment;

			string amount = paymentRequest.Payment.Amount.ToCents().ToString();
			string currency = paymentRequest.Amount.CurrencyIsoCode;

			var parameters = new Dictionary<string, string>
			{
				{"merchantnumber", merchantNumber},
				{
					"accepturl",
					new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder)
						.ToString()
				},
				{
					"cancelurl",
					new Uri(_absoluteUrlService.GetAbsoluteUrl(declineUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder)
						.ToString()
				},
				{"callbackurl", _callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment)},
				{"instantcallback", "1"},
				{"windowstate", "3"},
				{"orderid", paymentRequest.Payment.ReferenceId},
				{"amount", amount},
				{"currency", currency},
				{"ownreceipt", Convert.ToInt32(ownReceipt).ToString()},
				{"language", GetLanguageCode(paymentRequest)}
			};

			if (instantAcquire)
				parameters.Add("instantcapture", "1");

			if (splitPayment)
				parameters.Add("splitpayment", "1");

			return parameters;
		}

		/// <summary>
		/// Builds the form.
		/// </summary>
		/// <param name="page">The <see cref="StringBuilder" />.</param>
		/// <param name="paymentRequest">The payment request.</param>
		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append(@"<form id=""ePay"" name=""ePay"" method=""post"" action=""https://ssl.ditonlinebetalingssystem.dk/integration/ewindow/Default.aspx"">");

			var parameters = GetParameters(paymentRequest);

			bool useMd5 = paymentRequest.PaymentMethod.DynamicProperty<bool>().UseMd5;
			if (useMd5)
			{
				string key = paymentRequest.PaymentMethod.DynamicProperty<string>().Key.ToString();
				parameters.Add("hash", Md5Computer.GetPreMd5Key(parameters, key));
			}

			foreach (var parameter in parameters)
				AddHiddenField(page, parameter.Key, parameter.Value);

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");

			page.Append("</form>");
		}
	}
}

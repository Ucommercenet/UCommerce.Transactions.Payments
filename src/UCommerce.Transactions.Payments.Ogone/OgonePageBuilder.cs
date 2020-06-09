using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Ogone
{
	/// <summary>
	/// implementation of the OgonePageBuilder.
	/// Class is used to build a form, used to do a payment request at Ogone.
	/// </summary>
	public class OgonePageBuilder : AbstractPageBuilder
	{
		private readonly IAbsoluteUrlService _absoluteUrlService;
		public const string PaymentMethodPropertyName = "payment-method";
		public const string PaymentBrandPropertyName = "payment-brand";
		
		public OgonePageBuilder(IAbsoluteUrlService absoluteUrlService)
		{
			_absoluteUrlService = absoluteUrlService;
		}
		/// <summary>
		/// Builds the head attributes.
		/// </summary>
		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>Ogone</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if (!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		/// <summary>
		/// Builds the body attributes.
		/// </summary>
		/// <param name="page">The page.</param>
		/// <param name="paymentRequest">The payment request.</param>
		/// <remarks></remarks>
		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;
			string url = GetPostUrlForForm(testMode);

			page.Append(String.Format(@"<form method=""POST"" action=""{0}"">",url));

			var parameters = GetParameters(paymentRequest);
			AddParameters(page, paymentRequest, parameters);

			page.Append(@"</form>");

		}

		protected virtual void AddParameters(StringBuilder page, PaymentRequest paymentRequest, IDictionary<string, string> sortedDictionary)
		{
			foreach (var pair in sortedDictionary)
			{
				if (pair.Key == "post") continue; // Do not add a hidden field for the "post" entry, used when Debug is True.
				AddHiddenField(page, pair.Key, pair.Value);
			}

			if (Debug)
				AddSubmitButton(page, "post", "Post");
		}

		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			var paymentMethod = paymentRequest.PaymentMethod;
			var billingAddress = paymentRequest.PurchaseOrder.BillingAddress;
			
			//dynamic properties
			string pspId = paymentMethod.DynamicProperty<string>().PspId;
			string shaSignIn = paymentMethod.DynamicProperty<string>().ShaSignIn;
			string accepturl = paymentMethod.DynamicProperty<string>().AcceptUrl;
			string cancelurl = paymentMethod.DynamicProperty<string>().CancelUrl;
			string backurl = paymentMethod.DynamicProperty<string>().BackUrl;
			string declineurl = paymentMethod.DynamicProperty<string>().DeclineUrl;
			string exceptionurl = paymentMethod.DynamicProperty<string>().ExceptionUrl;
			bool instantAcquire = paymentMethod.DynamicProperty<bool>().InstantAcquire;

			var dictionary = new SortedDictionary<string, string>();
			
			dictionary["pspid"] = pspId;
			dictionary["orderId"] = paymentRequest.Payment.ReferenceId;
			dictionary["amount"] = paymentRequest.Payment.Amount.ToCents().ToString();
			dictionary["currency"] = paymentRequest.Amount.CurrencyIsoCode;
			dictionary["language"] = paymentRequest.PurchaseOrder.OgoneCultureCode();
			dictionary["cn"] = billingAddress.FullCustomerName();
			dictionary["email"] = billingAddress.EmailAddress;
			dictionary["ownerzip"] = billingAddress.PostalCode;
			dictionary["owneraddress"] = billingAddress.AddressLines();
			dictionary["ownercty"] = billingAddress.Country.TwoLetterISORegionName;
			dictionary["ownertown"] = billingAddress.City;
			dictionary["ownertelno"] = billingAddress.PhoneNumber;
			dictionary["operation"] = GetOperationParameter(instantAcquire);

			dictionary["pm"] = GetPaymentMethodParameter(paymentRequest);
			if (!string.IsNullOrWhiteSpace(paymentRequest.Payment[PaymentBrandPropertyName]))
			{
				dictionary["brand"] = paymentRequest.Payment[PaymentBrandPropertyName];
			}

			dictionary["accepturl"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(accepturl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			dictionary["cancelurl"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelurl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			dictionary["backurl"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(backurl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			dictionary["declineurl"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(declineurl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			dictionary["exceptionurl"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(exceptionurl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			
			dictionary["paramvar"] = GetParamVarForCallbackUrl(paymentRequest);

//			if (Debug)
//				dictionary["post"] = "post";

			var signature = CalculateSha1Signature(shaSignIn, dictionary);
			dictionary["SHASign"] = signature;
			
			return dictionary;
		}

		protected virtual string GetParametersStringForHash(string shaSign, IDictionary<string, string> parameters)
		{
			var builder = new StringBuilder();
			foreach (var parameter in parameters)
			{
				if (string.IsNullOrEmpty(parameter.Value))
					continue;

				builder.Append(parameter.Key.ToUpper() + "=" + parameter.Value + shaSign);
			}

			return builder.ToString();
		}

		private string CalculateSha1Signature(string shaSignIn, IDictionary<string, string> keys)
		{
			var oGoneSha1Computer = new OgoneSha1Computer();

			string parametersStringForHash = GetParametersStringForHash(shaSignIn,keys);
			string hashValue = oGoneSha1Computer.ComputeHash(parametersStringForHash);

			return hashValue.ToUpper();
		}

		private string GetOperationParameter(bool instantAcquire)
		{
			return instantAcquire ? "SAL" : "RES";
		}

		private string GetPostUrlForForm(bool testMode)
		{
			return testMode ? "https://secure.ogone.com/ncol/test/orderstandard.asp" : "https://secure.ogone.com/ncol/prod/orderstandard.asp";
		}

		public virtual string GetParamVarForCallbackUrl(PaymentRequest paymentRequest)
		{
			/**
			 * paramVarValue is sent with the form to tell Ogone which PaymentMethodID and paymentID is to be used in the server to server call
			 * when Ogone makes a callback to the Paymentprocessor.
			 * In Ogone back-end "http://yoursite.com/<PARAMVAR>/paymentproccessor.axd" is typed in their post-payment URL. 
			 * <PARAMVAR> is replaced by paramVarValue by Ogone server which leads to the right URL.
			**/
			return string.Format("{0}/{1}", paymentRequest.Payment.PaymentMethod.PaymentMethodId, paymentRequest.Payment.PaymentId);
		}

		protected virtual string GetPaymentMethodParameter(PaymentRequest paymentRequest)
		{
			return
				!string.IsNullOrWhiteSpace(paymentRequest.Payment[PaymentMethodPropertyName])
					? paymentRequest.Payment[PaymentMethodPropertyName]
					: "CreditCard";
		}
	}
}

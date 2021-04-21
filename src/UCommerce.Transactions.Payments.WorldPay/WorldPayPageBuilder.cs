using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.WorldPay
{
	public class WorldPayPageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;
		public WorldPayMd5Computer Md5Computer { get; set; }

		private const string URL_SANDBOX = "https://secure-test.worldpay.com/wcc/purchase";
		private const string URL_PRODUCTION = "https://secure.worldpay.com/wcc/purchase";

		public string GetPostUrl(PaymentMethod paymentMethod)
		{
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			return testMode ? URL_SANDBOX : URL_PRODUCTION;
		}

		public WorldPayPageBuilder(ICallbackUrl callbackUrl, WorldPayMd5Computer md5Computer)
		{
			_callbackUrl = callbackUrl;
			Md5Computer = md5Computer;
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>WorldPay</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if (!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append(@"<form method=""post"" action=""" + GetPostUrl(paymentRequest.PaymentMethod) + @""">");

			var parameters = GetParameters(paymentRequest);

			foreach (var parameter in parameters)
			{
				AddHiddenField(page, parameter.Key, parameter.Value);
			}

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");

			page.Append("</form>");
		}

		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			PaymentMethod paymentMethod = paymentRequest.PaymentMethod;
			Payment payment = paymentRequest.Payment;
			PurchaseOrder order = payment.PurchaseOrder;

			var dict = new Dictionary<string, string>();
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			bool instantCapture = paymentMethod.DynamicProperty<bool>().InstantCapture;
			string callback = paymentMethod.DynamicProperty<string>().Callback;
			string instId = paymentMethod.DynamicProperty<string>().InstId;
			string key = paymentMethod.DynamicProperty<string>().Key;
			string signature = paymentMethod.DynamicProperty<string>().Signature;

			// https://developer.worldpay.com/docs/bg350/send-order-details
			// All required fields
			var amount = paymentRequest.Payment.Amount.ToString("0.00", CultureInfo.InvariantCulture);
			var currency = paymentRequest.Amount.CurrencyIsoCode;

			dict.Add("testMode", (testMode ? 100 : 0).ToString());

			// Specifies the authorisation mode used. The values are "A" for a full auth, or "E" for a pre-auth.
			dict.Add("authMode", instantCapture ? "A" : "E");

			var hash = Md5Computer.GetHash(payment.Amount, payment.ReferenceId, payment.PurchaseOrder.BillingCurrency.ISOCode, key);
			var cartId = paymentRequest.Payment.ReferenceId;
			var callbackUrl = _callbackUrl.GetCallbackUrl(callback, paymentRequest.Payment);

			dict.Add("MC_hash", hash);
			dict.Add("instId", instId);

			dict.Add("cartId", cartId);
			dict.Add("amount", amount);
			dict.Add("currency", currency);

			// <!-- The below parameters are all optional in this submission, but some become mandatory on the payment page -->
			var billingAddress = order.BillingAddress;
			dict.Add("address1", billingAddress.Line1); // "1st line of address
			dict.Add("address2", billingAddress.Line2); // "2nd line of address
														//dict.Add("address3", billingAddress); // "3rd line of address
			dict.Add("town", billingAddress.City); // "Town
			dict.Add("region", billingAddress.State); // "Region/County/State
			dict.Add("postcode", billingAddress.PostalCode); // "Post/Zip Code
			dict.Add("country", billingAddress.Country.TwoLetterISORegionName); // "Country
																				//dict.Add("desc", order); // "Description of purchase
																				//dict.Add("resultfile", order); // "Final landing Page for shopper
																				//dict.Add("accId1", order); // "Tells us which merchant code to use (if more than one)
																				//dict.Add("authValidFrom", order); // "Time window for purchase to complete (from)
																				//dict.Add("authValidTo", order); // "Time window for purchase to complete (to)
			dict.Add("name", $"{billingAddress.FirstName} {billingAddress.LastName}"); // "Shopper's full name <!-- Accepts test values to simulate transaction outcome-->
			dict.Add("tel", billingAddress.PhoneNumber); // "Shopper's telephone number
														 //dict.Add("fax", order); // "Shopper's fax number
			dict.Add("email", billingAddress.EmailAddress); // "Shopper's email address


			// <!-- The below optional parameters control the behaviour of the payment pages -->
			dict.Add("fixContact", string.Empty); // "Prevents contact details from being edited
			dict.Add("hideContact", string.Empty); // "Hides contact details
			dict.Add("hideCurrency", string.Empty); // "Hides the currency drop down
			dict.Add("lang", order.CultureCode); // "Shopper's language choice
			dict.Add("noLanguageMenu", string.Empty); // "Hides the language menu
													  //dict.Add("withDelivery", order); // "Displays and mandates delivery address fields


			// <!-- This optional parameter is for testing, only relevant if you're creating your own messages files -->
			if (Debug)
				dict.Add("subst", "yes"); // "Lets you see the names of message properties


			// <!-- The below optional parameters can be used in the payment pages -->
			//dict.Add("amountString", order); // "HTML string produced from the amount and currency submitted
			dict.Add("countryString", billingAddress.Country.Name); // "Full name of the country produced from the country code
																	//dict.Add("compName", order); // "Name of the company



			// <!-- The below optional parameters instruct us to return data in the transaction result -->
			//dict.Add("transId", order); // "Worldpay's ID for the transaction
			//dict.Add("futurePayId", order); // "Worldpay's ID for a FuturePay (recurring) agreement, if applicable
			//dict.Add("transStatus", order); // "Result of this transaction
			//dict.Add("transTime", order); // "Time of this transaction
			//dict.Add("authAmount", order); // "Amount the transaction was authorised for
			//dict.Add("authCurrency", order); // "The currency used for authorisation
			//dict.Add("authAmountString", order); // "HTML string produced from auth amount and currency
			//dict.Add("rawAuthMessage", order); // "Text received from bank
			//dict.Add("rawAuthCode", order); // "Single character authorisation code
			//dict.Add("callbackPW", order); // "Payment Responses password, if set
			//dict.Add("cardType", order); // "Type of card used by the shopper
			//dict.Add("countryMatch", order); // "Result of comparison between shopper's country and card issuer country
			//dict.Add("AVS", order); // "Address Verification Service result


			//// <!-- The below optional parameters are prefixes for types of custom parameter -->
			//dict.Add("C_", order); // "Prefix for parameters only used in the result page
			//dict.Add("M_", order); // "Prefix for parameters only used in the payment responses
			//dict.Add("MC_", order); // "Prefix for parameters used in the both the result page and payment responses
			//dict.Add("CM_", order); // "Prefix for parameters used in the both the result page and payment responses


			dict.Add("signature", CalculateSignature(instId, amount, currency, cartId, callbackUrl, signature));
			dict.Add("MC_callback", callbackUrl);

			return dict;
		}

		private string CalculateSignature(string instId, string amount, string currency, string cartId, string callbackUrl, string signature)
		{
			// instId:amount:currency:cartId:MC_callback

			IList<string> signatureList = new List<string>();
			signatureList.Add(instId);
			signatureList.Add(amount);
			signatureList.Add(currency);
			signatureList.Add(cartId);
			signatureList.Add(callbackUrl);

			var signatureHash = Md5Computer.GetSignatureHash(signatureList, signature);
			return signatureHash;
		}
	}
}
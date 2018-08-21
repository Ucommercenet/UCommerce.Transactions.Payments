using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;

namespace UCommerce.Transactions.Payments.SecureTrading
{
	/// <summary>
	/// Provides functionality to build the form needed to post to SecureTrading:
	/// reference guide: http://www.securetrading.com/files/documentation/STPP-Payment-Pages-Setup-Guide.pdf
	/// </summary>
	public class SecureTradingPageBuilder : AbstractPageBuilder
	{
		private readonly SecureTradingMd5Computer _md5Computer;

		public SecureTradingPageBuilder(SecureTradingMd5Computer md5Computer)
		{
			_md5Computer = md5Computer;
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>Secure Trading</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if (!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			IDictionary<string, string> dictionary = GetParameters(paymentRequest);
			page.Append(@"<form method=""POST"" action=""https://payments.securetrading.net/process/payments/choice"">");

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");

			foreach (var pair in dictionary)
			{
				AddHiddenField(page, pair.Key, pair.Value);
			}

			page.Append("</form>");
		}

		/// <summary>
		/// Builds requried parameters Check reference guide page 29 for required fields.
		/// </summary>
		/// <param name="paymentRequest"></param>
		/// <returns></returns>
		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			string key = paymentRequest.PaymentMethod.DynamicProperty<string>().Key;
			bool instantCapture = paymentRequest.PaymentMethod.DynamicProperty<bool>().InstantCapture;

			IDictionary<string, string> dictionary = new Dictionary<string, string>();

			AddRequiredFieldsForPaymentRequest(paymentRequest, dictionary); //Those are the required fields for the initial payment request.
			AddSettlementStatus(dictionary, instantCapture);
			//AddBillingFieldsForPaymentRequest(paymentRequest, dictionary);
			AddHashedSecurityCheck(dictionary, key);
			AddCustomFieldsForAuthRequest(dictionary, paymentRequest.PaymentMethod);
			return dictionary;
		}

		/// <summary>
		/// Adds a custom field to the parameters list
		/// </summary>
		/// <remarks>Adding AuthRequestParameter allows to return it in the redirect to payment processor. Hence we can distinquish an auth request in process paymentrequest.
		/// Allows for implementing notifications at a later point.
		/// </remarks>
		private void AddCustomFieldsForAuthRequest(IDictionary<string, string> dictionary, PaymentMethod paymentMethod)
		{
			bool instantCapture = paymentMethod.DynamicProperty<bool>().InstantCapture;

			string authValue = instantCapture 
				? SecureTradingConstants.InstantCapture
				: SecureTradingConstants.Authorize;
			dictionary.Add(SecureTradingConstants.AuthRequestParameter, authValue);
		}

		/// <summary>
		/// The hidden field sitesecurity is a hashed field with a secret password and some parameters hashed together to prevent fraud. 
		/// The order of the values in the hash has to follow Secure Tradings guidelines.
		/// http://www.securetrading.com/files/documentation/STPP-Payment-Pages-Setup-Guide.pdf page 35
		/// </summary>
		private void AddHashedSecurityCheck(IDictionary<string, string> dictionary, string key)
		{
			//Apparently they want the hash prefixed with g
			dictionary.Add("sitesecurity", "g" + GetMd5Hash(dictionary, key));
		}

		/// <summary>
		/// Gets a hashed value for parameters supplied in the right order that you setup with secure trading.
		/// The values that you supply are not fixed. You need to contact support to set that up. We've set them up in the order below.
		/// http://www.securetrading.com/files/documentation/STPP-Payment-Pages-Setup-Guide.pdf page 35
		/// </summary>
		/// <returns></returns>
		private string GetMd5Hash(IDictionary<string, string> dictionary, string key)
		{
			var parametersForHash = new StringBuilder();

			parametersForHash.Append(dictionary["currencyiso3a"]);
			parametersForHash.Append(dictionary["mainamount"]);
			parametersForHash.Append(dictionary["orderreference"]);
			parametersForHash.Append(dictionary["sitereference"]);
			parametersForHash.Append(dictionary["settlestatus"]);

			parametersForHash.Append(key);

			return _md5Computer.GetComputedMd5Hash(parametersForHash.ToString());
		}

		/// <summary>
		/// SettleStatus is Secure tradings way of deffering on "instant capture".
		/// Per default the value is 0, which means that the money will be acquired right away - which in their setup is the following day.
		/// Setting this value to "2" suspends the settlement, thus this money can be acquired within seven days. After that the transaction will be canceled.
		/// </summary>
		private void AddSettlementStatus(IDictionary<string, string> dictionary, bool instantCapture)
		{
			//set settlestatus = 2 if you want to settle at a later point in time e.g do not use instant capture.
			dictionary.Add("settlestatus", instantCapture
				? ((int)SecureTradingSettlementStatus.PendingSettlement).ToString(CultureInfo.InvariantCulture)
				: ((int)SecureTradingSettlementStatus.Suspended).ToString(CultureInfo.InvariantCulture));
		}

		private void AddBillingFieldsForPaymentRequest(PaymentRequest paymentRequest, IDictionary<string, string> dictionary)
		{
			var billingAddress = paymentRequest.PurchaseOrder.BillingAddress;

			dictionary.Add("billingfirstname", billingAddress.FirstName);
			dictionary.Add("billinglastname", billingAddress.LastName);
			dictionary.Add("billingcountryiso2a", billingAddress.Country.TwoLetterISORegionName);
			dictionary.Add("billingemail", billingAddress.EmailAddress);
			dictionary.Add("billingtelephone", billingAddress.PhoneNumber);

			dictionary.Add("billingstreet", billingAddress.Line1);
			dictionary.Add("billingtown", billingAddress.City);
			dictionary.Add("billingcounty", billingAddress.State);
			dictionary.Add("billingpostcode", billingAddress.PostalCode);
		}

		/// <summary>
		/// Those are the minimum required fields (orderreference is not, but it links to the purchaseorder beeing linked with this payment).
		/// </summary>
		/// <param name="paymentRequest"></param>
		/// <param name="dictionary"></param>
		private void AddRequiredFieldsForPaymentRequest(PaymentRequest paymentRequest, IDictionary<string, string> dictionary)
		{
			string sitereference = paymentRequest.PaymentMethod.DynamicProperty<string>().Sitereference;

			dictionary.Add("sitereference", sitereference);
			dictionary.Add("currencyiso3a", paymentRequest.PurchaseOrder.BillingCurrency.ISOCode);
			dictionary.Add("mainamount", (FormatAmountForRequest(paymentRequest)));
			dictionary.Add("version", "1"); //As described in their docs, this value will be set to 1.
			dictionary.Add("orderreference", paymentRequest.PurchaseOrder.OrderGuid.ToString());
			dictionary.Add("paymentreference", paymentRequest.Payment["paymentGuid"]);
		}

		private string FormatAmountForRequest(PaymentRequest paymentRequest)
		{
			return paymentRequest.Amount.Value.ToString("F",CultureInfo.InvariantCulture);
		}
	}
}

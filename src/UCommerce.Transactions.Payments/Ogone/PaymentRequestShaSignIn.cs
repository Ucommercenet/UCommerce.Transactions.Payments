using System;
using System.Text;
using UCommerce.EntitiesV2;
using UCommerce.Transactions.Payments.Common;

namespace UCommerce.Transactions.Payments.Ogone
{
	/// <summary>
	/// Builds a string to hash when a payment which needs to be authorized or instant captured.
	/// Ogone needs a "sha sign" which is a signature used to verify that the data is identically in both ends.
	/// The "sha sign" is a sha-1 hash of the string this class builds.
	/// </summary>
	public class PaymentRequestShaSignIn
	{

		/// <summary>
		/// Builds the string to hash when a payment is requested. 
		/// Method is used in the OgonePageBuilder.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <param name="shaSignInParam">The sha sign in as configured at Ogone back-end.</param>
		/// <param name="acceptUrl">The accept URL.</param>
		/// <param name="backUrl">The back URL.</param>
		/// <param name="cancelUrl">The cancel URL.</param>
		/// <param name="declineUrl">The decline URL.</param>
		/// <param name="exceptionUrl">The exception URL.</param>
		/// <param name="operation">The operation value that tells Ogone what to do in the maintenance request.</param>
		/// <param name="pspid">The pspid of the Ogone account.</param>
		/// <returns>The string to be hashed and send with the payment request.</returns>
		public string BuildHashString(PaymentRequest paymentRequest, string shaSignIn, string acceptUrl, 
			string backUrl, string cancelUrl, string declineUrl, string exceptionUrl, string operation, string pspid)
		{
			var concatString = new StringBuilder();
			OrderAddress billingAddress = paymentRequest.PurchaseOrder.BillingAddress;

			concatString.Append(BuildStringSection("accepturl", acceptUrl, shaSignIn));
			concatString.Append(BuildStringSection("amount", paymentRequest.Amount.Value.ToCents().ToString(), shaSignIn));
			concatString.Append(BuildStringSection("backurl", backUrl, shaSignIn));
			concatString.Append(BuildStringSection("cancelurl", cancelUrl, shaSignIn));
			concatString.Append(BuildStringSection("cn", billingAddress.FullCustomerName(),shaSignIn));
			concatString.Append(BuildStringSection("currency", paymentRequest.Amount.Currency.ISOCode, shaSignIn));
			concatString.Append(BuildStringSection("declineurl", declineUrl, shaSignIn));
			concatString.Append(BuildStringSection("email", billingAddress.EmailAddress, shaSignIn));
			concatString.Append(BuildStringSection("exceptionurl", exceptionUrl, shaSignIn));
			concatString.Append(BuildStringSection("language", paymentRequest.PurchaseOrder.CultureCode, shaSignIn));
			concatString.Append(BuildStringSection("operation", operation, shaSignIn));
			concatString.Append(BuildStringSection("orderid", paymentRequest.Payment.ReferenceId, shaSignIn));
			concatString.Append(BuildStringSection("owneraddress", billingAddress.AddressLines(), shaSignIn));
			concatString.Append(BuildStringSection("ownercty", billingAddress.Country.Name, shaSignIn));
			concatString.Append(BuildStringSection("ownertelno", billingAddress.PhoneNumber, shaSignIn));
			concatString.Append(BuildStringSection("ownertown", billingAddress.City, shaSignIn));
			concatString.Append(BuildStringSection("ownerzip", billingAddress.PostalCode, shaSignIn));

			var paramVarValue = string.Format("{0}/{1}", paymentRequest.Payment.PaymentMethod.PaymentMethodId,
														paymentRequest.Payment.PaymentId);

			concatString.Append(BuildStringSection("paramvar", paramVarValue, shaSignIn));
			
			concatString.Append(BuildStringSection("pspid", pspid, shaSignIn));

			return concatString.ToString();
		}

		/// <summary>
		/// Builds a section of the string to hash. 
		/// Method helps to match the rules of building a sha sign given by Ogone.
		/// Keys must be in upper and empty values must be ignored.
		/// </summary>
		/// <param name="parameter">The parameter.</param>
		/// <param name="value">The value.</param>
		/// <param name="shaSign">The sha sign.</param>
		/// <returns>A string section in "KEY=value" format.</returns>
		private string BuildStringSection(string parameter, string value, string shaSign)
		{
			if (string.IsNullOrEmpty(value))
				return "";
			return parameter.ToUpper() + "=" + value + shaSign;
		}



	}
}

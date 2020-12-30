using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using Braintree;
using Ucommerce.Extensions;
using Ucommerce.Web;
using Environment = Braintree.Environment;

namespace Ucommerce.Transactions.Payments.Braintree
{
	public class BraintreePageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;

		public BraintreePageBuilder(ICallbackUrl callbackUrl)
		{
			_callbackUrl = callbackUrl;
		}

		/// <summary>
		/// It only uses to BuildBody to build the page from a html file.
		/// </summary>
		/// <param name="paymentRequest"></param>
		/// <returns></returns>
		public override string Build(PaymentRequest paymentRequest)
		{
			var page = new StringBuilder();
			BuildBody(page, paymentRequest);
			return page.ToString();
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymenrRequest)
		{
			//no action intended
		}

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;
			string merchantId = paymentRequest.PaymentMethod.DynamicProperty<string>().MerchantId;
			string publicKey = paymentRequest.PaymentMethod.DynamicProperty<string>().PublicKey;
			string privateKey = paymentRequest.PaymentMethod.DynamicProperty<string>().PrivateKey;
			string callbackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string paymentFormTemplate = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentFormTemplate;

			AddPaymentForm(page, paymentFormTemplate);
			
			var environment = testMode ? Environment.SANDBOX : Environment.PRODUCTION;
			var gateway = new BraintreeGateway(environment, merchantId, publicKey, privateKey);
			var clientToken = gateway.ClientToken.Generate();
			page.Replace(@"##CLIENT_TOKEN##", $@"{clientToken}");

			var callbackUrlWithPayment = _callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment);
			page.Replace(@"##CALLBACK_URL##", $@"{callbackUrlWithPayment}");
			
			AddThreeDSecureParameters(page, paymentRequest);

			//Braintree will redirect back in case of errors and we will have to display them.
			string errorMessages = HttpContext.Current.Request.QueryString["errorMessage"];
			if (!string.IsNullOrEmpty(errorMessages))
			{
				var errorMessageBuilder = new StringBuilder();
				foreach (string errorMessage in errorMessages.Split(';'))
					errorMessageBuilder.Append($"<li>{errorMessage}</li>");
				page.Replace(@"##ERROR_MESSAGES##", $@"<ul>{errorMessageBuilder}</ul>");
			}
			else
				page.Replace(@"##ERROR_MESSAGES##", "");
		}

		/// <summary>
		/// Reads the paymentFormTemplate into the page StringBuilder
		/// </summary>
		/// <param name="page">The page</param>
		/// <param name="paymentFormTemplate">The template to use</param>
		private void AddPaymentForm(StringBuilder page, string paymentFormTemplate)
		{
			using (var streamReader = new StreamReader(HttpContext.Current.Server.MapPath(string.Format(@"{0}", paymentFormTemplate))))
			{
				while (!streamReader.EndOfStream)
					page.AppendLine(streamReader.ReadLine());
			}
		}
		
		/// <summary>
        /// To use 3DS, you will need to pass an object with relevant customer and transaction data.
		/// into the threeDSecure field of the requestPaymentMethod options in order to minimize the need
		/// for issuing banks to present authentication challenges to customers.
		///
		/// This object must contain the `amount` field.
        /// </summary>
        /// <param name="page"></param>
        /// <param name="paymentRequest"></param>
        private void AddThreeDSecureParameters(StringBuilder page, PaymentRequest paymentRequest)
        {
            var billingAddress = paymentRequest.PurchaseOrder.BillingAddress;
            
            var threeDSecureParameters = new Dictionary<string, object>()
            {
	            // Braintree throws an error "Invalid Amount", if the amount has more than two digits.
                ["amount"] = Math.Round(paymentRequest.Payment.Amount, 2),
                ["email"] = billingAddress.EmailAddress,
                ["billingAddress"] = new Dictionary<string, string>()
                {
                    ["givenName"] = billingAddress.FirstName,
                    ["surname"] = billingAddress.LastName,
                    ["streetAddress"] = billingAddress.Line1,
                    ["extendedAddress"] = billingAddress.Line2,
                    ["locality"] = billingAddress.City,
                    ["postalCode"] = billingAddress.PostalCode,
                    ["phoneNumber"] = billingAddress.PhoneNumber
                }
            };

            var jsSerializer = new JavaScriptSerializer();
            var json = jsSerializer.Serialize(threeDSecureParameters);
            page.Replace(@"##THREE_D_SECURE_PARAMETERS##", $@"{json}");
        }

		/// <summary>
		/// Create a html option with value and text 
		/// </summary>
		/// <param name="values"></param>
		/// <returns></returns>
		private string CreateSelectOptions(IEnumerable<string> values)
		{
			var stringBuilder = new StringBuilder();
			foreach (var value in values)
				stringBuilder.Append($"<option value=\"{value}\" >{value}</option>");
			return stringBuilder.ToString();
		}
	}
}
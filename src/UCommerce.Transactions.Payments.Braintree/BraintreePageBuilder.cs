using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;
using Braintree;
using Ucommerce.EntitiesV2;
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

			var gateway = new BraintreeGateway
							  {
								  Configuration =
									  new global::Braintree.Configuration(
									  testMode ? Environment.SANDBOX : Environment.PRODUCTION,
									  merchantId, publicKey, privateKey)
							  };

			AddPaymentForm(page, paymentFormTemplate);

			page.Replace(@"##ACTIONURL##", string.Format(@"{0}", gateway.TransparentRedirect.Url));

			OrderAddress billingAddress = paymentRequest.Payment.PurchaseOrder.BillingAddress;

			//Braintree code will convert decimal to string using current thread culture, but reqiures invarinat format.
			//Set culture temporarily in 'en-us'
			var currentCulture = Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");
			string trData = gateway.Transaction.SaleTrData(
				new TransactionRequest
					{
						Amount = Math.Round(paymentRequest.Payment.Amount, 2), // Braintree throws an error "Invalid Amount", if the amount has more than two digits.
						OrderId = paymentRequest.Payment.ReferenceId,
						PurchaseOrderNumber = string.IsNullOrEmpty(paymentRequest.Payment.PurchaseOrder.OrderNumber) ? "" : paymentRequest.Payment.PurchaseOrder.OrderNumber,
						BillingAddress =
							new AddressRequest
								{
									FirstName = billingAddress.FirstName,
									LastName = billingAddress.LastName,
									StreetAddress = billingAddress.Line1,
									ExtendedAddress = billingAddress.Line2,
									PostalCode = billingAddress.PostalCode,
									CountryName = billingAddress.Country.Name,
									Company = billingAddress.CompanyName
								},
					},
				_callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment));
			Thread.CurrentThread.CurrentCulture = currentCulture;
			page.Replace(@"##TRDATA##", string.Format(@"{0}", trData));

			page.Replace(@"##EXPMONTH##", string.Format(@"{0}", CreateSelectOptions(new[] { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12" })));

			var years = new string[10];
			for (int i = 0; i < 10; i++)
				years[i] = (DateTime.Now.Year + i).ToString(CultureInfo.InvariantCulture);
			page.Replace(@"##EXPYEAR##", string.Format(@"{0}", CreateSelectOptions(years)));

			//Braintree will redirect back in case of errors and we will have to display them.
			string errorMessages = HttpContext.Current.Request.QueryString["errorMessage"];
			if (!string.IsNullOrEmpty(errorMessages))
			{
				var errorMessageBuilder = new StringBuilder();
				foreach (string errorMessage in errorMessages.Split(';'))
					errorMessageBuilder.Append(string.Format("<li>{0}</li>", errorMessage));
				page.Replace(@"##ERRORMESSAGES##", string.Format(@"<ul>{0}</ul>", errorMessageBuilder));
			}
			else
				page.Replace(@"##ERRORMESSAGES##", "");
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
		/// Create a html option with value and text 
		/// </summary>
		/// <param name="values"></param>
		/// <returns></returns>
		private string CreateSelectOptions(IEnumerable<string> values)
		{
			var stringBuilder = new StringBuilder();
			foreach (var value in values)
				stringBuilder.Append(string.Format("<option value=\"{0}\" >{0}</option>", value));
			return stringBuilder.ToString();
		}
	}
}

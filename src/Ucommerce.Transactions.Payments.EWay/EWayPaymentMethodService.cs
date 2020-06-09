using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.EWay
{
	/// <summary>
	/// eWay payment provider http://eway.com.au.
	/// </summary>
	public class EWayPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private readonly ICallbackUrl _callbackUrl;

		public EWayPaymentMethodService(IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl)
		{
			_absoluteUrlService = absoluteUrlService;
			_callbackUrl = callbackUrl;
		}

		/// <summary>
		/// Processes the payment resonse. 
		/// Sets the paymentstatus to either acquired or declined. 
		/// </summary>
		/// <param name="payment">The payment request.</param>
		public override void ProcessCallback(Payment payment)
		{
			Guard.Against.PaymentNotPendingAuthorization(payment);

			var paymentMethod = payment.PaymentMethod;
			string acceptUrl = paymentMethod.DynamicProperty<string>().AcceptUrl;
			string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;

			EWayAuthorizationResponse eWayResponse = VerifyTransactionWithEWay(payment);
			PaymentStatusCode paymentStatus = eWayResponse.TransactionSuccessful ? PaymentStatusCode.Acquired : PaymentStatusCode.Declined;

			payment.PaymentStatus = PaymentStatus.Get((int) paymentStatus);
			payment.TransactionId = eWayResponse.TransactionNumber;
			payment["AuthCode"] = eWayResponse.AuthCode;

			if (paymentStatus == PaymentStatusCode.Acquired)
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

			HttpContext.Current.Response.Redirect(paymentStatus == PaymentStatusCode.Acquired
													? new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString()
			                                      	: new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
		}

		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			throw new NotImplementedException("Acquire not supported from EWay payment provider.");
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			throw new NotImplementedException("Cancel not supported from EWay payment provider.");
		}

		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			throw new NotImplementedException("Refund not supported by EWay payment provider.");
		}

		/// <summary>
		/// Makes a placeholder transaction and redirects to eWay payment site. 
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>Payment</returns>
		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			string response = CreatePlaceholderPayment(paymentRequest);

			var reader = XDocument.Parse(response);

			bool transactionAccepted = GetTransactionAcceptedStatus(reader);

			string url = GetRedirectUrl(reader);

			Guard.Against.TransactionNotAccepted(transactionAccepted, reader);

			Guard.Against.EmptyRedirectUrl(url);

            HttpContext.Current.Response.Redirect(url);

			return paymentRequest.Payment;
		}

		private string GetRedirectUrl(XDocument reader)
		{
			string url = "";
			var uri = reader.Descendants("URI").FirstOrDefault();
			if (uri != null)
				url = uri.Value;
			return url;
		}

		private bool GetTransactionAcceptedStatus(XDocument reader)
		{
			bool transactionAccepted = false;
			var result = reader.Descendants("Result").FirstOrDefault();
			if (result != null)
				transactionAccepted = result.Value.Equals("True");

			return transactionAccepted;
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotSupportedException("EWay does not need a local form. Use RequestPayment instead.");
		}

		private string UrlEncodeString(string input)
		{
			if (input == null) return null;

			string[] strings = input.Split(new[] {' '});

			var encodedList = new List<string>();

			foreach (string item in strings)
			{
				encodedList.Add(HttpUtility.UrlEncode(item));
			}

			return string.Join("+", encodedList.ToArray());
		}

		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			var parameters = new Dictionary<string, string>();

			Payment payment = paymentRequest.Payment;
			var paymentMethod = payment.PaymentMethod;

			string customerId = paymentMethod.DynamicProperty<string>().CustomerId;
			string userName = paymentMethod.DynamicProperty<string>().UserName;
			string callbackUrl = paymentMethod.DynamicProperty<string>().CallbackUrl;
			string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;

			parameters.Add("CustomerID", customerId);
			parameters.Add("UserName", userName);
			parameters.Add("Amount", paymentRequest.Amount.Value.ToInvariantString());

			string currencyIsoCode = paymentRequest.Amount.CurrencyIsoCode;
			if (currencyIsoCode.ToUpper() != "AUD")
				throw new NotSupportedException("Only Australian dollars is supported by Eway. Please make sure to use Currency AUD.");
			parameters.Add("Currency", currencyIsoCode);

			parameters.Add("ReturnURL", _callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment));
			parameters.Add("CancelURL", cancelUrl);

			OrderAddress billingAddress = payment.PurchaseOrder.GetBillingAddress();
			parameters.Add("CustomerFirstName", UrlEncodeString(billingAddress.FirstName));
			parameters.Add("CustomerLastName", UrlEncodeString(billingAddress.LastName));
			parameters.Add("CustomerAddress",
					 UrlEncodeString(billingAddress.Line1 +
					 (String.IsNullOrEmpty(billingAddress.Line2) ? "" : ", " + billingAddress.Line2)));
			parameters.Add("CustomerCity", UrlEncodeString(billingAddress.City));
			parameters.Add("CustomerState", UrlEncodeString(billingAddress.State));
			parameters.Add("CustomerPostCode", UrlEncodeString(billingAddress.PostalCode));
			parameters.Add("CustomerCountry", UrlEncodeString(billingAddress.Country.Name));
			parameters.Add("CustomerPhone", UrlEncodeString(billingAddress.PhoneNumber));
			parameters.Add("CustomerEmail", UrlEncodeString(billingAddress.EmailAddress));

			return parameters;
		}

		protected virtual string CreatePlaceholderPayment(PaymentRequest paymentRequest)
		{
			var parameters = GetParameters(paymentRequest);

			string requestUrl = "https://au.ewaygateway.com/Request";
			string response = RequestEWay(requestUrl, parameters);
			if (string.IsNullOrEmpty(response))
				throw new InvalidOperationException("XML response from EWay empty. Please check RequestURL:" + requestUrl);
			return response;
		}

		private EWayAuthorizationResponse VerifyTransactionWithEWay(Payment payment)
		{
			IDictionary<string, string> dict = new Dictionary<string, string>();

			var paymentMethod = payment.PaymentMethod;

			string customerId = paymentMethod.DynamicProperty<string>().CustomerId;
			string userName = paymentMethod.DynamicProperty<string>().UserName;	
			
			dict.Add("CustomerID", customerId);
			dict.Add("UserName", userName);
			string accessPaymentCode = HttpContext.Current.Request["AccessPaymentCode"];
			if (string.IsNullOrEmpty(accessPaymentCode))
				throw new ArgumentException(@"AccessPaymentCode must be present in query string.");
			dict.Add("AccessPaymentCode", accessPaymentCode);


			string response = RequestEWay("https://au.ewaygateway.com/Result", dict);
			if (string.IsNullOrEmpty(response))
				throw new InvalidDataException("Transaction response string must contain XML confirmation data.");
			var reader = new XDocument();
			reader = XDocument.Parse(response);
			bool transactionOk = Convert.ToBoolean(reader.Descendants("TrxnStatus").First().Value);
			string transactionNumber = reader.Descendants("TrxnNumber").First().Value;
			string authCode = reader.Descendants("AuthCode").First().Value;
			string responseMessage = reader.Descendants("TrxnResponseMessage").First().Value;

			return new EWayAuthorizationResponse
			       	{
			       		TransactionSuccessful = transactionOk,
			       		TransactionNumber = transactionNumber,
			       		AuthCode = authCode,
						ResponseMessage = responseMessage
			       	};
		}


		private string RequestEWay(string url, IDictionary<string, string> formValuesToPost)
		{
			url += addQueryString(formValuesToPost);
			var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
			httpWebRequest.Method = "GET";
			return new StreamReader(httpWebRequest.GetResponse().GetResponseStream(), Encoding.GetEncoding("utf-8")).ReadToEnd();
		}

		/// <summary>
		/// Adds the query string.
		/// </summary>
		/// <param name="dictionary">The dictionary.</param>
		/// <returns></returns>
		/// <remarks></remarks>
		private string addQueryString(IDictionary<string, string> dictionary)
		{
			return "?"+string.Join("&", (dictionary).Select((a => a.Key + "=" + a.Value)).ToArray());			
		}
	}

	internal class EWayAuthorizationResponse
	{
		public bool TransactionSuccessful { get; set; }
		public string TransactionNumber { get; set; }
		public string AuthCode { get; set; }
		public string ResponseMessage { get; set; }
	}
}
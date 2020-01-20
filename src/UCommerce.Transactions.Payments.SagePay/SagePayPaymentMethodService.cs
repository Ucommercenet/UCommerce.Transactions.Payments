using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.SagePay
{
	/// <summary>
	/// Implementation of the SagePay payment provider.
	/// </summary>
	public class SagePayPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly INumberSeriesService _numberSeriesService;
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private readonly ICallbackUrl _callbackUrl;
		private SagePayMd5Computer Md5Computer { get; set; }

		/// <summary>
		/// Protocol version used.
		/// </summary>
		protected virtual string PROTOCOL_VERSION
		{
			get { return "2.23"; }	
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PayExPaymentMethodService"/> class.
		/// </summary>
		public SagePayPaymentMethodService(SagePayMd5Computer md5Computer, INumberSeriesService numberSeriesService, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl)
		{
			_numberSeriesService = numberSeriesService;
			_absoluteUrlService = absoluteUrlService;
			_callbackUrl = callbackUrl;
			Md5Computer = md5Computer;
		}

		private string UrlEncodeString(string input)
		{
			if (input == null) return null;

			var strings = input.Split(new char[] { ' ' });

			var encodedList = new List<string>();

			foreach (var item in strings)
			{
				encodedList.Add(HttpUtility.UrlEncode(item));
			}

			return string.Join("+", encodedList.ToArray());
		}

		protected virtual IDictionary<string, string> AddParameters(PaymentRequest paymentRequest)
		{
			string successUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().SuccessUrl;
			string failureUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().FailureUrl;
			string notificationUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().NotificationUrl;
			string vendor = paymentRequest.PaymentMethod.DynamicProperty<string>().Vendor;
			string txType = paymentRequest.PaymentMethod.DynamicProperty<string>().TxType;

			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			IDictionary<string, string> dictionary = new Dictionary<string, string>();

			var payment = paymentRequest.Payment;
			var billingAddress = payment.PurchaseOrder.GetBillingAddress();

			string country = billingAddress.Country.TwoLetterISORegionName;
			string surname = UrlEncodeString(billingAddress.LastName);
			string firstnames = UrlEncodeString(billingAddress.FirstName);
			string address1 = UrlEncodeString(billingAddress.Line1);
			string address2 = UrlEncodeString(billingAddress.Line2);
			string city = UrlEncodeString(billingAddress.City);
			string postcode = UrlEncodeString(billingAddress.PostalCode);
			string email = UrlEncodeString(billingAddress.EmailAddress);
			string phone = UrlEncodeString(billingAddress.PhoneNumber);
			string state = UrlEncodeString(billingAddress.State);

			dictionary.Add("VendorTxCode", payment.ReferenceId);
			dictionary.Add("Amount", payment.Amount.ToString("0.00", CultureInfo.InvariantCulture));
			dictionary.Add("Currency", paymentRequest.Amount.Currency.ISOCode);
			dictionary.Add("Description", PaymentMessages.PurchaseDescription);

			dictionary.Add("SuccessURL", new Uri(_absoluteUrlService.GetAbsoluteUrl(successUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
			dictionary.Add("FailureURL", new Uri(_absoluteUrlService.GetAbsoluteUrl(failureUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
			dictionary.Add("NotificationURL", _callbackUrl.GetCallbackUrl(notificationUrl, payment));
			dictionary.Add("SendEMail", "0");
			dictionary.Add("Apply3DSecure", "2");

			dictionary.Add("BillingFirstnames", firstnames);
			dictionary.Add("BillingSurname", surname);
			dictionary.Add("BillingAddress1", address1);
			dictionary.Add("BillingAddress2", address2);
			dictionary.Add("BillingCity", city);
			dictionary.Add("BillingPostCode", postcode);
			dictionary.Add("BillingCountry", country);
			dictionary.Add("BillingPhone", phone);
			if (!string.IsNullOrEmpty(state))
				dictionary.Add("BillingState", state);
			dictionary.Add("CustomerEMail", email);

			// Get shipping address if one exists, otherwise use billing address
			var deliveryAddress = billingAddress;
			var shipment = payment.PurchaseOrder.Shipments.FirstOrDefault();
			if (shipment != null)
				deliveryAddress = shipment.ShipmentAddress ?? billingAddress;

			country = deliveryAddress.Country.TwoLetterISORegionName;
			surname = UrlEncodeString(deliveryAddress.LastName);
			firstnames = UrlEncodeString(deliveryAddress.FirstName);
			address1 = UrlEncodeString(deliveryAddress.Line1);
			city = UrlEncodeString(deliveryAddress.City);
			postcode = UrlEncodeString(deliveryAddress.PostalCode);
			phone = UrlEncodeString(deliveryAddress.PhoneNumber);
			state = UrlEncodeString(deliveryAddress.State);

			dictionary.Add("DeliverySurname", surname);
			dictionary.Add("DeliveryFirstnames", firstnames);
			dictionary.Add("DeliveryAddress1", address1);
			dictionary.Add("DeliveryCity", city);
			dictionary.Add("DeliveryPostCode", postcode);
			dictionary.Add("DeliveryCountry", country);
			dictionary.Add("DeliveryPhone", phone);
			if (!string.IsNullOrEmpty(state))
				dictionary.Add("DeliveryState", state);

			dictionary.Add("VPSProtocol", PROTOCOL_VERSION);
			dictionary.Add("TxType", txType);
			dictionary.Add("Vendor", vendor);

			return dictionary;
		}

		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			var response = RequestHttpPost(paymentRequest);
			var payment = paymentRequest.Payment;

			string status = GetField("Status", response);
			string strStatusDetail = GetField("StatusDetail", response);

			var statusCode = GetStatus(status);
			switch (statusCode)
			{
				case SagePayStatusCode.Ok:
					var field = GetField("NextURL", response);
					var getField = GetField("SecurityKey", response);
					payment.SetSagePaymentInfo(FieldCode.SecurityKey, getField);
					payment.Save();

                    HttpContext.Current.Response.Redirect(field);
					break;
				default:
					throw new Exception(string.Format("Error: {0}, Message: {1}.", statusCode, strStatusDetail));
			}

			return paymentRequest.Payment;
		}

		protected virtual string RequestHttpPost(PaymentRequest paymentRequest)
		{
			var payment = paymentRequest.Payment;
			var url = GetSystemURL("purchase", payment.PaymentMethod);
			var dictionary = AddParameters(paymentRequest);

			HttpPost post = new HttpPost(url, dictionary);

			return post.GetString();
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotImplementedException("SagePay provider doesn't need a form post for integration.");
		}

		/// <summary>
		/// Parses the <paramref name="statusString"/> to a <see cref="SagePayStatusCode"/>.
		/// </summary>
		/// <param name="statusString">The status.</param>
		/// <returns></returns>
		protected SagePayStatusCode GetStatus(string statusString)
		{
			switch (statusString.ToUpper())
			{
				case "AUTHENTICATED":
					return SagePayStatusCode.Authenticated;
				case "REJECTED":
					return SagePayStatusCode.Rejected;
				case "OK":
					return SagePayStatusCode.Ok;
				case "REGISTERED":
					return SagePayStatusCode.Registered;
				case "ERROR":
					return SagePayStatusCode.Error;
				case "ABORT":
					return SagePayStatusCode.Abort;
				case "MALFORMED":
					return SagePayStatusCode.Malformed;
				case "INVALID":
					return SagePayStatusCode.Invalid;
				default:
					return SagePayStatusCode.Unknown;
			}
		}

		/// <summary>
		/// Gets the field value from the <paramref name="response"/> string. Each pair should have the format "Name=value" seperated by a newline.
		/// </summary>
		/// <param name="fieldName">Name of the field.</param>
		/// <param name="response">The response.</param>
		/// <returns></returns>
		public static string GetField(string fieldName, string response)
		{
			var strings = response.Split(Environment.NewLine.ToCharArray());

			var line = strings.FirstOrDefault(a => a.StartsWith(fieldName, StringComparison.InvariantCultureIgnoreCase));

			if (line == null)
				throw new Exception(string.Format(@"Could not find FieldName: ""{0}"" in ""{1}""", fieldName, response));

			return line.Substring(fieldName.Length + 1);
		}

		/// <summary>
		/// Gets the list of parameters used for the encryption signature.
		/// </summary>
		/// <param name="request">The request context.</param>
		/// <param name="securityKey">The security key.</param>
		/// <param name="paymentMethod">The payment method</param>
		/// <returns></returns>
		protected virtual IList<string> GetSignatureParameterList(HttpRequest request, string securityKey, PaymentMethod paymentMethod)
		{
			string vendor = paymentMethod.DynamicProperty<string>().Vendor;

			IList<string> list = new List<string>();

			list.Add(GetHttpRequestValueUrlDecoded(request, "VPSTxId"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "VendorTxCode"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "Status"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "TxAuthNo"));
			list.Add(vendor.ToLower());
			list.Add(GetHttpRequestValueUrlDecoded(request, "AVSCV2"));
			list.Add(securityKey);
			list.Add(GetHttpRequestValueUrlDecoded(request, "AddressResult"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "PostCodeResult"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "CV2Result"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "GiftAid"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "3DSecureStatus"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "CAVV"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "AddressStatus"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "PayerStatus"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "CardType"));
			list.Add(GetHttpRequestValueUrlDecoded(request, "Last4Digits"));

			return list;
		}

		/// <summary>
		/// Gets the HTTP request value URL decoded.
		/// </summary>
		/// <param name="request">The request context.</param>
		/// <param name="name">The parameter name.</param>
		/// <returns></returns>
		protected string GetHttpRequestValueUrlDecoded(HttpRequest request, string name)
		{
			return HttpUtility.UrlDecode(request[name] ?? "");
		}

		/// <summary>
		/// Processed the callback received from the payment provider.
		/// </summary>
		/// <param name="payment">The payment.</param>
		public override void ProcessCallback(Payment payment)
		{
			string abortUrl = payment.PaymentMethod.DynamicProperty<string>().AbortUrl;
			string successUrl = payment.PaymentMethod.DynamicProperty<string>().SuccessUrl;
			string failureUrl = payment.PaymentMethod.DynamicProperty<string>().FailureUrl;
			string txType = payment.PaymentMethod.DynamicProperty<string>().TxType;

			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
				return;

			HttpRequest request = HttpContext.Current.Request;
			IList<string> list = GetSignatureParameterList(request, payment.GetSagePaymentInfo(FieldCode.SecurityKey),payment.PaymentMethod);

			string vpsSignature = Md5Computer.VpsSignature(list).ToUpper();

			var orgVpsSignature = request["VPSSignature"];

			if (string.IsNullOrEmpty(orgVpsSignature))
				throw new Exception("VPSSignature is null in the HttpRequest.");

			if (!vpsSignature.Equals(orgVpsSignature))
				throw new Exception(string.Format("VPS Signatures are not equal, Calculated Signature: {0}, From HttpRequest: {1}.", vpsSignature, orgVpsSignature));

			string vpsTxId = request["VPSTxId"];
			string txAuthNo = request["TxAuthNo"];

			if (string.IsNullOrEmpty(vpsTxId))
				throw new ArgumentException(@"vpsTxId must be present in query string.");

			var statusCodeString = request["Status"];

			if (string.IsNullOrEmpty(statusCodeString))
				throw new Exception("NullOrEmptystatus response from SagePay.");

			var status = GetStatus(statusCodeString);

			var authNo = GetHttpRequestValueUrlDecoded(request, "TxAuthNo");
			var txId = GetHttpRequestValueUrlDecoded(request, "VPSTxId");

			payment.SetSagePaymentInfo(FieldCode.VPSTxId, txId);
			payment.SetSagePaymentInfo(FieldCode.TxAuthNo, authNo);

			IDictionary<string, string> results = new Dictionary<string, string>();
			switch (status)
			{
				case SagePayStatusCode.Ok:
				case SagePayStatusCode.Registered:
				case SagePayStatusCode.Authenticated:
					if (txType.Equals("PAYMENT", StringComparison.InvariantCultureIgnoreCase))
						payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
					else
						payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);

					ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

					results.Add("Status", "OK");
					results.Add("RedirectURL", new Uri(_absoluteUrlService.GetAbsoluteUrl(successUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
					results.Add("StatusDetail", "All went well.");
					break;
				case SagePayStatusCode.Error:
					results.Add("Status", "INVALID");
					results.Add("RedirectURL", new Uri(_absoluteUrlService.GetAbsoluteUrl(failureUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
					results.Add("StatusDetail", string.Format("Error: {0}.", status));
					break;
				case SagePayStatusCode.Malformed:
				case SagePayStatusCode.Invalid:
				case SagePayStatusCode.Unknown:
				case SagePayStatusCode.Rejected:
					results.Add("Status", "OK");
					results.Add("RedirectURL", new Uri(_absoluteUrlService.GetAbsoluteUrl(failureUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
					results.Add("StatusDetail", string.Format("Error: {0}.", status));
					break;
				case SagePayStatusCode.Abort:
					results.Add("Status", "OK");
					results.Add("RedirectURL", new Uri(_absoluteUrlService.GetAbsoluteUrl(abortUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
					results.Add("StatusDetail", string.Format("Error: {0}.", status));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			payment.Save();

			WriteResponse(results);
		}

		/// <summary>
		/// Writes the <paramref name="dictionary"/> to the response in "Key=Value" form.
		/// </summary>
		/// <param name="dictionary">The dictionary.</param>
		private void WriteResponse(IDictionary<string, string> dictionary)
		{
			HttpContext.Current.Response.Clear();
			HttpContext.Current.Response.ContentType = "text/plain";
			foreach (var item in dictionary)
			{
				HttpContext.Current.Response.Write(string.Format("{0}={1}{2}", item.Key, item.Value, Environment.NewLine));
			}
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			string vendor = payment.PaymentMethod.DynamicProperty<string>().Vendor;

			var systemUrl = GetSystemURL("authorise",payment.PaymentMethod);

			IDictionary<string, string> dict = new Dictionary<string, string>();
			dict.Add("VPSProtocol", PROTOCOL_VERSION);
			dict.Add("TxType", "Authorise");
			dict.Add("Vendor", vendor);
			dict.Add("Amount", payment.Amount.ToString("0.00", CultureInfo.InvariantCulture));

			var paymnetReference = _numberSeriesService.GetNumber("Default Payment Reference");

			dict.Add("VendorTxCode", paymnetReference);
			dict.Add("Description", PaymentMessages.PurchaseDescription);
			dict.Add("RelatedVPSTxId", payment.GetSagePaymentInfo(FieldCode.VPSTxId));
			dict.Add("RelatedVendorTxCode", payment.ReferenceId);
			dict.Add("RelatedSecurityKey", payment.GetSagePaymentInfo(FieldCode.SecurityKey));
			dict.Add("ApplyAVSCV2", "0");

			var post = new HttpPost(systemUrl, dict);
			var response = post.GetString();

			status = GetField("StatusDetail", response);
			var stringStatus = GetField("status", response);
			var getStatus = GetStatus(stringStatus);
			switch (getStatus)
			{
				case SagePayStatusCode.Ok:
					payment.SetSagePaymentInfo(FieldCode.VPSTxId, GetField("VPSTxId", response));
					payment.SetSagePaymentInfo(FieldCode.TxAuthNo, GetField("TxAuthNo", response));
					payment.SetSagePaymentInfo(FieldCode.SecurityKey, GetField("SecurityKey", response));
					payment.ReferenceId = paymnetReference;
					payment.Save();
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			string vendor = payment.PaymentMethod.DynamicProperty<string>().Vendor;

			var systemUrl = GetSystemURL("refund",payment.PaymentMethod);

			IDictionary<string, string> dict = new Dictionary<string, string>();
			dict.Add("VPSProtocol", PROTOCOL_VERSION);
			dict.Add("TxType", "REFUND");
			dict.Add("Vendor", vendor);
			dict.Add("Amount", payment.Amount.ToString("0.00", CultureInfo.InvariantCulture));
			dict.Add("Currency", payment.PurchaseOrder.BillingCurrency.ISOCode);

			var paymentReference = _numberSeriesService.GetNumber("Default Payment Reference");

			dict.Add("VendorTxCode", paymentReference);
			dict.Add("Description", PaymentMessages.PurchaseDescription);
			dict.Add("RelatedVPSTxId", payment.GetSagePaymentInfo(FieldCode.VPSTxId));
			dict.Add("RelatedVendorTxCode", payment.ReferenceId);
			dict.Add("RelatedSecurityKey", payment.GetSagePaymentInfo(FieldCode.SecurityKey));
			dict.Add("RelatedTxAuthNo", payment.GetSagePaymentInfo(FieldCode.TxAuthNo));

			var post = new HttpPost(systemUrl, dict);
			var response = post.GetString();

			status = GetField("StatusDetail", response);
			var stringStatus = GetField("status", response);
			var getStatus = GetStatus(stringStatus);
			switch (getStatus)
			{
				case SagePayStatusCode.Ok:
					payment.SetSagePaymentInfo(FieldCode.VPSTxId, GetField("VPSTxId", response));

					payment.ReferenceId = paymentReference;
					payment.Save();
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			string vendor = payment.PaymentMethod.DynamicProperty<string>().Vendor;

			var systemUrl = GetSystemURL("cancel",payment.PaymentMethod);

			IDictionary<string, string> dict = new Dictionary<string, string>();
			dict.Add("VPSProtocol", PROTOCOL_VERSION);
			dict.Add("TxType", "CANCEL");
			dict.Add("Vendor", vendor);
			dict.Add("VendorTxCode", payment.ReferenceId);
			dict.Add("VPSTxId", payment.GetSagePaymentInfo(FieldCode.VPSTxId));
			dict.Add("SecurityKey", payment.GetSagePaymentInfo(FieldCode.SecurityKey));

			var post = new HttpPost(systemUrl, dict);
			var response = post.GetString();

			status = GetField("StatusDetail", response);
			var stringStatus = GetField("status", response);
			var getStatus = GetStatus(stringStatus);
			switch (getStatus)
			{
				case SagePayStatusCode.Ok:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Gets the sagepay url.
		/// </summary>
		/// <param name="strType">Type of the STR.</param>
		/// <param name="paymentMethod">The payment method.</param>
		/// <returns></returns>
		protected virtual string GetSystemURL(string strType, PaymentMethod paymentMethod)
		{
			string testMode = paymentMethod.DynamicProperty<string>().TestMode;

			string strSystemURL = "";

			if (testMode.ToUpper() == "LIVE")
			{
				switch (strType.ToLower())
				{
					case "abort":
						strSystemURL = "https://live.sagepay.com/gateway/service/abort.vsp";
						break;
					case "authorise":
						strSystemURL = "https://live.sagepay.com/gateway/service/authorise.vsp";
						break;
					case "cancel":
						strSystemURL = "https://live.sagepay.com/gateway/service/cancel.vsp";
						break;
					case "purchase":
						strSystemURL = "https://live.sagepay.com/gateway/service/vspserver-register.vsp";
						break;
					case "refund":
						strSystemURL = "https://live.sagepay.com/gateway/service/refund.vsp";
						break;
					case "release":
						strSystemURL = "https://live.sagepay.com/gateway/service/release.vsp";
						break;
					case "repeat":
						strSystemURL = "https://live.sagepay.com/gateway/service/repeat.vsp";
						break;
					case "void":
						strSystemURL = "https://live.sagepay.com/gateway/service/void.vsp";
						break;
					case "3dcallback":
						strSystemURL = "https://live.sagepay.com/gateway/service/direct3dcallback.vsp";
						break;
					case "showpost":
						strSystemURL = "https://test.sagepay.com/showpost/showpost.asp";
						break;
				}
			}
			else if (testMode.ToUpper() == "TEST")
			{
				switch (strType.ToLower())
				{
					case "abort":
						strSystemURL = "https://test.sagepay.com/gateway/service/abort.vsp";
						break;
					case "authorise":
						strSystemURL = "https://test.sagepay.com/gateway/service/authorise.vsp";
						break;
					case "cancel":
						strSystemURL = "https://test.sagepay.com/gateway/service/cancel.vsp";
						break;
					case "purchase":
						strSystemURL = "https://test.sagepay.com/gateway/service/vspserver-register.vsp";
						break;
					case "refund":
						strSystemURL = "https://test.sagepay.com/gateway/service/refund.vsp";
						break;
					case "release":
						strSystemURL = "https://test.sagepay.com/gateway/service/release.vsp";
						break;
					case "repeat":
						strSystemURL = "https://test.sagepay.com/gateway/service/repeat.vsp";
						break;
					case "void":
						strSystemURL = "https://test.sagepay.com/gateway/service/void.vsp";
						break;
					case "3dcallback":
						strSystemURL = "https://test.sagepay.com/gateway/service/direct3dcallback.vsp";
						break;
					case "showpost":
						strSystemURL = "https://test.sagepay.com/showpost/showpost.asp";
						break;
				}
			}
			else
			{
				switch (strType.ToLower())
				{
					case "abort":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorAbortTx";
						break;
					case "authorise":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorAuthoriseTx";
						break;
					case "cancel":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorCancelTx";
						break;
					case "purchase":
						strSystemURL = "https://test.sagepay.com/simulator/VSPServerGateway.asp?Service=VendorRegisterTx";
						break;
					case "refund":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorRefundTx";
						break;
					case "release":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorReleaseTx";
						break;
					case "repeat":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorRepeatTx";
						break;
					case "void":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverGateway.asp?Service=VendorVoidTx";
						break;
					case "3dcallback":
						strSystemURL = "https://test.sagepay.com/simulator/vspserverCallback.asp";
						break;
					case "showpost":
						strSystemURL = "https://test.sagepay.com/showpost/showpost.asp";
						break;
				}
			}
			return strSystemURL;
		}
	}
}
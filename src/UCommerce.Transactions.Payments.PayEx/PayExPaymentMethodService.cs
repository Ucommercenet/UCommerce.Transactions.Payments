using System;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Environment;
using Ucommerce.Infrastructure.Globalization;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Transactions.Payments.PayEx.PayEx.PayExBackendService;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.PayEx
{
	/// <summary>
	/// Implementation of the http://payex.com payment provider.
	/// </summary>
	public class PayExPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private readonly ICallbackUrl _callbackUrl;
		private readonly IWebRuntimeInspector _webRuntimeInspector;
		private ILocalizationContext LocalizationContext { get; set; }
		private PayExMd5Computer Md5Computer { get; set; }

		private PxOrderSoapClient Client { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PayExPaymentMethodService"/> class.
		/// </summary>
		public PayExPaymentMethodService(ILocalizationContext localizationContext, PayExMd5Computer md5Computer, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl, IWebRuntimeInspector webRuntimeInspector)
		{
			_absoluteUrlService = absoluteUrlService;
			_callbackUrl = callbackUrl;
			_webRuntimeInspector = webRuntimeInspector;
			LocalizationContext = localizationContext;
			Md5Computer = md5Computer;
		}

		protected virtual PxOrderSoapClient GetPxOrderSoapClient(PaymentMethod paymentMethod)
		{
			var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
			{
				CloseTimeout = TimeSpan.FromMinutes(1),
				OpenTimeout = TimeSpan.FromMinutes(1),
				ReceiveTimeout = TimeSpan.FromMinutes(10),
				SendTimeout = TimeSpan.FromMinutes(10),
				AllowCookies = false,
				BypassProxyOnLocal = false,
				HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
				MaxBufferSize = 65536,
				MaxBufferPoolSize = 524288,
				MaxReceivedMessageSize = 65536,
				MessageEncoding = WSMessageEncoding.Text,
				TextEncoding = Encoding.UTF8,
				TransferMode = TransferMode.Buffered,
				UseDefaultWebProxy = true
			};

			Uri uri = paymentMethod.DynamicProperty<bool>().TestMode
				? new Uri("https://test-external.payex.com/pxorder/pxorder.asmx", UriKind.Absolute)
				: new Uri("https://external.payex.com/pxorder/pxorder.asmx", UriKind.Absolute);

			var endpointAddress = new EndpointAddress(uri);

			Client = new PxOrderSoapClient(binding, endpointAddress);
			return Client;
		}

		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			var paymentMethod = paymentRequest.PaymentMethod;

			string callBackUrl = paymentMethod.DynamicProperty<string>().CallbackUrl;
			string cancelUrlForPaymentMethod = paymentMethod.DynamicProperty<string>().CancelUrl;
			long accountNumber = Convert.ToInt64(paymentMethod.DynamicProperty<string>().AccountNumber.ToString());
			string key = paymentMethod.DynamicProperty<string>().Key;
			
			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			var vatRate = Convert.ToInt32(paymentRequest.Payment.PurchaseOrder.OrderLines.First().VATRate * 100) * 100;
			var price = paymentRequest.Payment.Amount.ToCents();

			const string purchaseOperation = "AUTHORIZATION";
			var isoCode = paymentRequest.PurchaseOrder.BillingCurrency.ISOCode;
			var productNumber = paymentRequest.Payment.ReferenceId;
			var clientIpAddress = HttpContext.Current.Request.UserHostAddress;
			const string description = "Sum";
			var returnUrl = _callbackUrl.GetCallbackUrl(callBackUrl, paymentRequest.Payment);
			const string creditcard = "CREDITCARD";
			var clientLanguage = LocalizationContext.CurrentCultureCode;

			string cancelUrl = AbstractPageBuilder.GetAbsoluteUrl(cancelUrlForPaymentMethod);

			string value =	accountNumber +
							purchaseOperation +
							price +
							"" +
							isoCode +
							vatRate +
							productNumber +
							productNumber +
							description +
							clientIpAddress +
							"" +
							"" +
							"" +
							returnUrl +
							creditcard +
							"" +
							cancelUrl +
							clientLanguage;

			
			var preHash = Md5Computer.GetPreHash(value,key);

			var pxOrderSoapClient = GetPxOrderSoapClient(paymentMethod);

			string xml = pxOrderSoapClient.Initialize7(
				accountNumber,
				purchaseOperation,
				price,
				"",
				isoCode,
				vatRate,
				productNumber,
				productNumber,
				description,
				clientIpAddress,
				"",
				"",
				"",
				returnUrl,
				creditcard,
				"",
				cancelUrl,
				clientLanguage,
				preHash
				);

			var message = new PayExXmlMessage(xml);

			if (!message.StatusCode)
				throw new Exception(string.Format("The webservice returned: {0}.", xml));

            // To avoid Thread being aborted exception allow the request to complete before redirecting.
            HttpContext.Current.Response.Redirect(message.RedirectUrl);
			
			return paymentRequest.Payment;
		}

		/// <summary>
		/// RenderPage is not used for PayEx.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>A string containing the html form.</returns>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Processed the callback received from the payment provider.
		/// </summary>
		/// <param name="payment">The payment.</param>
		public override void ProcessCallback(Payment payment)
		{
			Guard.Against.PaymentNotPendingAuthorization(payment);
			Guard.Against.NoHttpContext(_webRuntimeInspector);
			Guard.Against.MissingParameterInResponse("orderRef");

			var paymentMethod = payment.PaymentMethod;

			var acceptUrl = paymentMethod.DynamicProperty<string>().AcceptUrl;
			var cancelUrlForPaymentMethod = paymentMethod.DynamicProperty<string>().CancelUrl;
			long accountNumber = Convert.ToInt64(paymentMethod.DynamicProperty<string>().AccountNumber.ToString());
			var key = paymentMethod.DynamicProperty<string>().Key;
			
			string orderReference = GetOrderReferenceFromRequest();

			string hash = Md5Computer.GetPreHash(accountNumber + orderReference,key);

			PayExXmlMessage completeResponseMessage = CompletePayment(payment, accountNumber, orderReference, hash);

			Guard.Against.ResponseWasNotOk(completeResponseMessage, payment);
			Guard.Against.PaymentAlreadyCompleted(completeResponseMessage, payment);

			if (TransactionStatusNotAuthorized(completeResponseMessage))
			{
				HandleDeclinedResponse(payment, cancelUrlForPaymentMethod);
			}
			else
			{
				HandleAuthorizedResponse(payment, acceptUrl,completeResponseMessage.TransactionNumber.ToString());
			}
		}

		private PayExXmlMessage CompletePayment(Payment payment, long accountNumber, string orderReference, string hash)
		{
			var xml = GetPxOrderSoapClient(payment.PaymentMethod).Complete(accountNumber, orderReference, hash);

			var message = new PayExXmlMessage(xml);
			return message;
		}

		private string GetOrderReferenceFromRequest()
		{
			return HttpContext.Current.Request["orderRef"];
		}

		private void HandleAuthorizedResponse(Payment payment, string acceptUrl, string transactionId)
		{
			payment.TransactionId = transactionId;
			payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);

			ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

			HttpContext.Current.Response.Redirect(
				new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
		}

		private void HandleDeclinedResponse(Payment payment, dynamic cancelUrlForPaymentMethod)
		{
			payment.PaymentStatus = PaymentStatus.Get((int) PaymentStatusCode.Declined);
			payment.Save();

			HttpContext.Current.Response.Redirect(
				new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrlForPaymentMethod)).AddOrderGuidParameter(payment.PurchaseOrder)
					.ToString());
		}

		private bool TransactionStatusNotAuthorized(PayExXmlMessage message)
		{
			return message.TransactionStatus != 3;
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			var paymentMethod = payment.PaymentMethod;

			long accountNumber = Convert.ToInt64(paymentMethod.DynamicProperty<string>().AccountNumber.ToString());
			var key = paymentMethod.DynamicProperty<string>().Key;

			int transactionNumber;
			if(!int.TryParse(payment.TransactionId, out transactionNumber))
				throw new Exception(string.Format("Could not convert: {0} to an int.", payment.TransactionId));

			var amount = Convert.ToInt32(payment.Amount*100);
			var referenceId = payment.ReferenceId;

			var preHash = accountNumber.ToString() + transactionNumber + amount + referenceId;
			var hash = Md5Computer.GetPreHash(preHash,key);

			var xml = GetPxOrderSoapClient(payment.PaymentMethod).Capture3(accountNumber, transactionNumber, amount, referenceId, hash);

			var message = new PayExXmlMessage(xml);

			if (message.StatusCode && message.TransactionStatus == 6)
			{
				payment.TransactionId = message.TransactionNumber.ToString();
				status = PaymentMessages.AcquireSuccess;
				return true;	
			}
			
			status = PaymentMessages.AcquireFailed + ": " + message.ErrorDescription;
			return false;
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			var paymentMethod = payment.PaymentMethod;

			long accountNumber = Convert.ToInt64(paymentMethod.DynamicProperty<string>().AccountNumber.ToString());
			string key = paymentMethod.DynamicProperty<string>().Key.ToString();

			var amount = payment.Amount.ToCents();
			var transactionRef = int.Parse(payment.TransactionId);

			var hashInput = accountNumber + "" + transactionRef + "" + amount + "" + payment.ReferenceId;
			var preHash = Md5Computer.GetPreHash(hashInput,key);

			var xml = GetPxOrderSoapClient(payment.PaymentMethod).Credit3(accountNumber, transactionRef, amount, payment.ReferenceId, preHash);

			var message = new PayExXmlMessage(xml);

			if (message.StatusCode && message.TransactionStatus == 2)
			{
				status = PaymentMessages.RefundSuccess;
				payment.TransactionId = message.TransactionNumber.ToString();
				return true;
			}

			status = PaymentMessages.RefundFailed + ": " + message.ErrorDescription;
			return false;
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			var paymentMethod = payment.PaymentMethod;

			long accountNumber = Convert.ToInt64(paymentMethod.DynamicProperty<string>().AccountNumber.ToString());
			var key = paymentMethod.DynamicProperty<string>().Key;

			int transactionNumber;
			if (!int.TryParse(payment.TransactionId, out transactionNumber))
				throw new FormatException(string.Format("Could not convert: {0} to an int.", payment.TransactionId));

			var preHash = accountNumber.ToString() + transactionNumber;
			var hash = Md5Computer.GetPreHash(preHash,key);

			var xml = GetPxOrderSoapClient(payment.PaymentMethod).Cancel2(accountNumber, transactionNumber, hash);

			var message = new PayExXmlMessage(xml);

			if (message.StatusCode && message.TransactionStatus == 4 && message.OriginalTransactionNumber.ToString() == payment.TransactionId)
			{
				payment.TransactionId = message.TransactionNumber.ToString();
				payment.Save();
				status = PaymentMessages.CancelSuccess;
				return true;
			}

			status = PaymentMessages.CancelFailed + ": " + message.ErrorDescription;
			return false;
		}
	}
}
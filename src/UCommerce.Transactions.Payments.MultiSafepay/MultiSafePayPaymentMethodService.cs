using System;
using System.Net;
using System.Security;
using System.Web;
using System.Xml;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Logging;
namespace Ucommerce.Transactions.Payments.MultiSafepay
{
	/// <summary>
	/// MultiSafepay Connect payment method service.
	/// </summary>
	/// <remarks>Setup to use the MtultiSafepay Connect option.</remarks>
	public class MultiSafepayPaymentMethodService : ExternalPaymentMethodService, IRequireRedirect
	{
		private readonly ILoggingService _loggingService;
		public IOrderService OrderService { get; set; }
		public MultiSafepayPaymentRequestBuilder PaymentRequestBuilder { get; set; }
		public MultiSafepayStatusRequestBuilder StatusRequestBuilder { get; set; }
		public MultiSafepayHttpRequester HttpRequester { get; set; }

		public MultiSafepayPaymentMethodService(
			MultiSafepayPaymentRequestBuilder paymentRequestBuilder, 
			MultiSafepayStatusRequestBuilder statusRequestBuilder, 
			MultiSafepayHttpRequester httpRequester, 
			IOrderService orderService,
			ILoggingService loggingService)
		{
			_loggingService = loggingService;
			Guard.Against.NullArgument(orderService);
			Guard.Against.NullArgument(paymentRequestBuilder);
			Guard.Against.NullArgument(statusRequestBuilder);
			Guard.Against.NullArgument(httpRequester);

			OrderService = orderService;
			PaymentRequestBuilder = paymentRequestBuilder;
			StatusRequestBuilder = statusRequestBuilder;
			HttpRequester = httpRequester;
		}

		/// <summary>
		/// Renders the forms to be submitted to the payment provider.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>A string containing the html form.</returns>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotSupportedException("MultiSafePay does not need a local form. Use RequestPayment instead.");
		}

		/// <summary>
		/// Redirects to '/{PaymentMethodName}/{PaymentGuid}/PaymentRequest.axd', and lets the current page execution finish.
		/// </summary>
		/// <param name="paymentRequest">Information used to generate the URL to redirect to.</param>
		/// <returns></returns>
		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			string xmlRequestString = PaymentRequestBuilder.BuildRequest(paymentRequest);

			XmlElement xmlElement = HttpRequester.Request(xmlRequestString, paymentRequest.PaymentMethod);

			string result = xmlElement.Attributes.GetNamedItem("result").Value;

			if (result.ToLower() != "ok")
			{
				_loggingService.Debug<MultiSafepayPaymentMethodService>("Failing xml request: " + xmlRequestString);
				GuardAgainstIncorrectResponse(xmlElement);
			}

			XmlNode transactionIdXmlNode = xmlElement.SelectSingleNode("/redirecttransaction/transaction/id");
			if (transactionIdXmlNode == null)
				throw new NullReferenceException("Xml response didn't contain a transaction ID as expected. Try sending a new request.");

			XmlNode paymentUrlXmlNode = xmlElement.SelectSingleNode("/redirecttransaction/transaction/payment_url");
			if (paymentUrlXmlNode == null)
				throw new NullReferenceException("Xml response didn't contain a payment url as expected. Try sending a new request.");

			if (transactionIdXmlNode.InnerText != paymentRequest.Payment.ReferenceId)
				throw new SecurityException("Transaction ID doesn't match internal reference id.");
			
			paymentRequest.Payment["redirectUrl"] = paymentUrlXmlNode.InnerText;
            
			return paymentRequest.Payment;
		}

		/// <summary>
		/// Protects against incorrect response that doesnt correspond with expected code from MultiSafepay
		/// </summary>
		/// <param name="xmlElement">The XmlElement.</param>
		private void GuardAgainstIncorrectResponse(XmlElement xmlElement)
		{
			if (xmlElement.Attributes.GetNamedItem("result").Value != "error")
				throw new SecurityException("Unexpected responsecode. Response doesn't match with MultiSafepay templates.");

			XmlNode errorCodeXmlNode = xmlElement.SelectSingleNode("/*/error/code");
			if (errorCodeXmlNode == null)
				throw new NullReferenceException("The Xml error response doesn't contain an error code as expected.");

			XmlNode errorMessageXmlNode = xmlElement.SelectSingleNode("/*/error/description");
			if (errorMessageXmlNode == null)
				throw new NullReferenceException("The Xml error response doesn't contain an error description as expected.");

			throw new WebException(string.Format("Error: {0}. Description: {1}.", errorCodeXmlNode.InnerText, errorMessageXmlNode.InnerText));
		}

		/// <summary>
		/// Extracts the transaction ID from the notification sent by MultiSafepay
		/// </summary>
		/// <param name="httpRequest">The httpRequest object.</param>
		public override Payment Extract(HttpRequest httpRequest)
		{
			string transactionId = httpRequest["transactionid"];
			if (string.IsNullOrEmpty(transactionId))
				throw new NullReferenceException("The notification didn't contain a transaction ID as expected.");

			return Payment.SingleOrDefault(x => x.ReferenceId == HttpUtility.UrlDecode(transactionId));
		}

		/// <summary>
		/// Processed the callback received from the payment provider.
		/// </summary>
		/// <param name="payment">The payment.</param>
		public override void ProcessCallback(Payment payment)
		{
			//MultiSafePay will retry callback until an HTTP code 200 is received.
			//Ignore payments which could not be found by extractor.
			if (payment == null) return;

			string status = GetStatusRemotelyFromMultiSafePay(payment.ReferenceId, payment);

			switch (status)
			{
				//
				// Bank transfers go from Initiated -> Completed
				//                                  -> void
				// Credit cards go to either: Completed
				//                            Declined
				// When OrderStatusCode       Uncleared -> Completed
				// changes to .Processing               -> void
				// the BasketInformation
				// will be cleared
				//
				case "completed":
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
					if (payment.PurchaseOrder.OrderStatus.OrderStatusId == (int)OrderStatusCode.Processing || payment.PurchaseOrder.OrderStatus.OrderStatusId == (int)OrderStatusCode.Basket)
						ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
					break;
				case "uncleared":
					OrderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.Processing));
					break;
				case "initialized":
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.PendingAuthorization);
					OrderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.Processing));
					break;
				case "void":
					OrderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.RequiresAttention));
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Cancelled);
					break;
				case "declined":
					OrderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.RequiresAttention));
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Declined);
					break;
				case "refunded":
					OrderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.RequiresAttention));
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Refunded);
					break;
				case "expired":
					OrderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.RequiresAttention));
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.AcquireFailed);
					break;
				default:
					throw new InvalidOperationException(string.Format("Status change not an expected value from the MultiSafepay template. Received {0}.", status));
			}

			payment.Save();
		}

		/// <summary>
		/// Get the status of the transaction and saves the transaction ID from MultiSafepay
		/// </summary>
		/// <param name="transactionId">The transactionId.</param>
		/// <param name="payment">The payment object</param>
		/// <returns>A string containing the status</returns>
		private string GetStatusRemotelyFromMultiSafePay(string transactionId, Payment payment)
		{
			string statusRequest = StatusRequestBuilder.BuildRequest(transactionId, payment.PaymentMethod);

			XmlElement xmlElement = HttpRequester.Request(statusRequest, payment.PaymentMethod);

			string resultString = xmlElement.Attributes.GetNamedItem("result").Value;

			if (resultString.ToLower() != "ok")
				GuardAgainstIncorrectResponse(xmlElement);

			XmlNode transactionIdXmlNode = xmlElement.SelectSingleNode("/status/ewallet/id");
			if (transactionIdXmlNode == null)
				throw new NullReferenceException("Xml response from MultiSafepay doesn't contain a transaction ID. Expected transaction ID in status report.");
			if (payment.TransactionId == null)
				payment.TransactionId = transactionIdXmlNode.InnerText;

			XmlNode statusXmlNode = xmlElement.SelectSingleNode("/status/ewallet/status");
			if (statusXmlNode == null)
				throw new NullReferenceException("Xml response from MultiSafepay doesn't contain a status update. Expected update on transaction.");

			return statusXmlNode.InnerText;
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			throw new NotSupportedException("Remote cancel is not supported, use the backend office.");
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			throw new NotSupportedException("MultiSafePay use instant payment. Acquire payment is not supported.");
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			throw new NotSupportedException("Remote refund is not supported, use the backend office.");
		}

		public string GetRedirectUrl(Payment payment)
		{
			return payment["redirectUrl"];
		}
	}
}

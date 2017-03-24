using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.SecureTrading
{
	public class SecureTradingPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly ILoggingService _loggingService;
		private readonly IRepository<OrderStatus> _orderStatusRepository;
		private readonly IRepository<PaymentStatus> _paymentStatusRepository;
		private readonly IOrderService _orderService;
		private readonly AbstractPageBuilder _pageBuilder;
		private readonly IHttpPaymentExtractor _httpPaymentExtractor;
		private readonly SecureTradingXmlRequester _secureTradingXmlRequester;
		private readonly int _numberOfRetriesForTransactionQuery;
		private readonly int _secondsToWaitOnRetryForQuery;
		private readonly IAbsoluteUrlService _absoluteUrlService;

		public SecureTradingPaymentMethodService(
			ILoggingService loggingService, 
			IRepository<OrderStatus> orderStatusRepository, 
			IRepository<PaymentStatus> paymentStatusRepository,
			IOrderService orderService,
			SecureTradingPageBuilder pageBuilder, 
			IHttpPaymentExtractor httpExtractor, 
			SecureTradingXmlRequester secureTradingXmlRequester,
			int numberOfRetriesForTransactionQuery, 
			int secondsToWaitOnRetryForQuery,
			IAbsoluteUrlService absoluteUrlService)
		{
			_loggingService = loggingService;
			_orderStatusRepository = orderStatusRepository;
			_paymentStatusRepository = paymentStatusRepository;
			_orderService = orderService;
			_pageBuilder = pageBuilder;
			_httpPaymentExtractor = httpExtractor;
			_secureTradingXmlRequester = secureTradingXmlRequester;
			_numberOfRetriesForTransactionQuery = numberOfRetriesForTransactionQuery;
			_secondsToWaitOnRetryForQuery = secondsToWaitOnRetryForQuery;
			_absoluteUrlService = absoluteUrlService;
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return _pageBuilder.Build(paymentRequest);
		}

		public override Payment Extract(HttpRequest httpRequest)
		{
			return _httpPaymentExtractor.Extract(httpRequest);
		}

		private void RedirectToUrl(string url, PurchaseOrder purchaseOrder)
		{
			HttpContext.Current.Response.Redirect(
				new Uri(_absoluteUrlService.GetAbsoluteUrl(url)).AddOrderGuidParameter(purchaseOrder).ToString());
		}

		public override void ProcessCallback(Payment payment)
		{
			string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string declineUrl = payment.PaymentMethod.DynamicProperty<string>().DeclineUrl;
			bool instantCapture = payment.PaymentMethod.DynamicProperty<bool>().InstantCapture;

			var httpRequest = HttpContext.Current.Request;
			NameValueCollection queryString = httpRequest.QueryString;

			if (!RequestMatchesAuthRequest(queryString, instantCapture))
			{
				_loggingService.Log<SecureTradingPaymentMethodService>(
					string.Format("Request captured that didn't match auth request. Request contained following parameters:\r\n {0}", ReadQueryString(queryString)));
				return;
			}

			if (SettleRequestWasOk(queryString))
			{
				var success = HandleAuthRequest(payment, queryString);
				if (success)
				{
					ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
					RedirectToUrl(acceptUrl, payment.PurchaseOrder);
				}
				else
				{
					_orderService.ChangeOrderStatus(payment.PurchaseOrder,OrderStatus.Get((int)OrderStatusCode.RequiresAttention));
					RedirectToUrl(declineUrl, payment.PurchaseOrder);
				}
			}
			else if (SettleRequestWasDeclined(queryString))
			{
				HandleDeclinedAuthRequest(payment, queryString);
				RedirectToUrl(declineUrl, payment.PurchaseOrder);
			}
			else
			{
				HandleUnknownError(payment,queryString);
				RedirectToUrl(declineUrl, payment.PurchaseOrder);
			}
		}

		/// <summary>
		/// AuthRequestParameter Should be included in querystring when returning from Secure Trading payment gateway.
		/// </summary>
		protected virtual bool RequestMatchesAuthRequest(NameValueCollection queryString, bool instantCapture)
		{
			string valueToMatch = instantCapture 
				? SecureTradingConstants.InstantCapture 
				: SecureTradingConstants.Authorize;
			
			if (string.IsNullOrEmpty(queryString[SecureTradingConstants.AuthRequestParameter])) return false;

			return queryString[SecureTradingConstants.AuthRequestParameter] == valueToMatch;
		}

		private void HandleUnknownError(Payment payment, NameValueCollection queryString)
		{
			payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Declined);
			_orderService.ChangeOrderStatus(payment.PurchaseOrder,_orderStatusRepository.Get((int)OrderStatusCode.RequiresAttention));
			_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("Auth request for payment: {0} failed. Response was: {1}", payment.TransactionId, ReadQueryString(queryString)));
		}

		private void HandleDeclinedAuthRequest(Payment payment, NameValueCollection queryString)
		{	
			Guard.Against.NotPendingAuthorizationForPayment(payment,_paymentStatusRepository);

			payment.TransactionId = GetQueryStringParameter(queryString, SecureTradingConstants.Transactionreference);
			PaymentStatus paymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Declined);
			payment.PaymentStatus = paymentStatus;

			_orderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.RequiresAttention));
			_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("Auth request for payment: {0} failed. Response was: {1}", payment.TransactionId, ReadQueryString(queryString)));
		}

		private bool HandleAuthRequest(Payment payment, NameValueCollection queryString)
		{
			bool instantCapture = payment.PaymentMethod.DynamicProperty<bool>().InstantCapture;

			SecureTradingTransactionQueryXmlResponse transactionQueryResponse = GetSecureTradingXmlResponse(queryString, payment.PaymentMethod);
			
			if (!transactionQueryResponse.TransactionFound)
			{
				payment.TransactionId = GetQueryStringParameter(queryString, SecureTradingConstants.Transactionreference);
				return false;
			}

			Guard.Against.TransactionQueryRequestDoesNotMatchesAuthStatus(transactionQueryResponse);
			Guard.Against.TransactionRequestDoesNotMatchesOrder(payment, transactionQueryResponse);
			Guard.Against.NotPendingAuthorizationForPayment(payment, _paymentStatusRepository);

			payment.TransactionId = transactionQueryResponse.TransactionId;

			if (transactionQueryResponse.SettleStatus == SecureTradingSettlementStatus.Suspended && !instantCapture)
			{
				payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Authorized);
			}
			else if (transactionQueryResponse.SettleStatus == (int)SecureTradingSettlementStatus.PendingSettlement && instantCapture)
			{
				payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Acquired);
			}
			else
			{
				throw new InvalidOperationException(
					string.Format("SettleStatus did not match InstantCapture configuration. Was: {0} Configuration for InstantCapture: {1}", 
					transactionQueryResponse.SettleStatus,
					instantCapture));
			}
			return true;
		}

		private static string ReadQueryString(NameValueCollection querystring)
		{
			var builder = new StringBuilder();
			foreach (var key in querystring.AllKeys)
			{
				builder.Append(string.Format("{0}={1}&", key, querystring[key]));
			}

			return builder.ToString();
		}

		private bool SettleRequestWasDeclined(NameValueCollection queryString)
		{
			return GetQueryStringParameter(queryString, SecureTradingConstants.ErrorCode) == ((int)SecureTradingErrorCode.Declined).ToString(CultureInfo.InvariantCulture);
		}

		private bool SettleRequestWasOk(NameValueCollection queryString)
		{
			return GetQueryStringParameter(queryString, SecureTradingConstants.ErrorCode) == ((int)SecureTradingErrorCode.Success).ToString(CultureInfo.InvariantCulture);
		}

		private SecureTradingTransactionQueryXmlResponse GetSecureTradingXmlResponse(NameValueCollection queryString, PaymentMethod paymentMethod)
		{
			string transactionId = GetQueryStringParameter(queryString, SecureTradingConstants.Transactionreference);

			Guard.Against.NullOrEmptyString(transactionId, "No transactionId was present in the request.");

			return GetSafeTransactionQuery(transactionId, paymentMethod);
		}

		private SecureTradingTransactionQueryXmlResponse GetSafeTransactionQuery(string transactionId, PaymentMethod paymentMethod)
		{
			SecureTradingTransactionQueryXmlResponse secureTradingTransactionQueryXmlResponse = _secureTradingXmlRequester.TransactionQuery(transactionId, paymentMethod);
			bool transactionFound = secureTradingTransactionQueryXmlResponse.TransactionFound;
			if (transactionFound) return secureTradingTransactionQueryXmlResponse;

			_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("Transaction with id: {0} not found. \r\n. Waiting {1} seconds for retry.",transactionId,_secondsToWaitOnRetryForQuery));
			int retries = 0;
			while (!transactionFound && retries < _numberOfRetriesForTransactionQuery)
			{
				Thread.Sleep(_secondsToWaitOnRetryForQuery * 1000);
				secureTradingTransactionQueryXmlResponse = _secureTradingXmlRequester.TransactionQuery(transactionId, paymentMethod);
				transactionFound = secureTradingTransactionQueryXmlResponse.TransactionFound;
				retries++;
				if (!transactionFound)
					_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("Failed to find Transaction with id: {0}. Number of tries: {1} ",retries,transactionId));
			}

			if (transactionFound)
				_loggingService.Log<SecureTradingPaymentMethodService>(
					string.Format("Transaction was found after {0} number of tries.", retries));
			else
			{
				_loggingService.Log<SecureTradingPaymentMethodService>(
					string.Format("Failed to find transaction after {0} attempts with timeout period: {1}",_numberOfRetriesForTransactionQuery,_numberOfRetriesForTransactionQuery*_secondsToWaitOnRetryForQuery));
			}

			return secureTradingTransactionQueryXmlResponse;
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			SecureTradingTransactionQueryXmlResponse transactionQueryResponse = _secureTradingXmlRequester.TransactionQuery(payment.TransactionId, payment.PaymentMethod);
			
			Guard.Against.PaymentDoesNotQualifyForCancellation(transactionQueryResponse);

			SecureTradingTransactionUpdateXmlResponse cancelRequestResponse 
				= _secureTradingXmlRequester.UpdateSettleMentStatus(payment.TransactionId,SecureTradingSettlementStatus.Cancelled, payment.PaymentMethod);

			if (cancelRequestResponse.ErrorCode == SecureTradingErrorCode.Success)
			{
				payment.PaymentStatus = _paymentStatusRepository.Get((int)PaymentStatusCode.Cancelled);
				status = PaymentMessages.CancelSuccess;
				return true;
			}

			_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("failed to cancel payment. Message: {0} Response was:\r\n {1}",cancelRequestResponse.ErrorMessage, cancelRequestResponse.XmlResponse ));
			
			status = string.Format("{0} - {1}", PaymentMessages.CancelFailed, cancelRequestResponse.ErrorMessage);
			return false;
		}

		/// <summary>
		/// Acquires the payment if payment is authorized. 
		/// </summary>
		/// <param name="payment"></param>
		/// <param name="status"></param>
		/// <returns></returns>
		/// <remarks>http://www.securetrading.com/files/documentation/STPP-Transaction-Update.pdf
		/// page 9 describes the xml to be sent for updating the transaction. 
		/// </remarks>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			SecureTradingTransactionQueryXmlResponse transactionQueryResponse = _secureTradingXmlRequester.TransactionQuery(payment.TransactionId, payment.PaymentMethod);

			Guard.Against.PaymentStatusIsNotSuspended(transactionQueryResponse);

			SecureTradingTransactionUpdateXmlResponse updateSettleMentStatusResponse = 
				_secureTradingXmlRequester.UpdateSettleMentStatus(payment.TransactionId,SecureTradingSettlementStatus.PendingSettlement, payment.PaymentMethod);

			if (updateSettleMentStatusResponse.ErrorCode == SecureTradingErrorCode.Success)
			{
				status = PaymentMessages.AcquireSuccess;
				payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Acquired);
				return true;
			}

			_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("failed to acquire payment. Message: {0} Response was:\r\n {1}", updateSettleMentStatusResponse.ErrorMessage, updateSettleMentStatusResponse.XmlResponse));
			payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.AcquireFailed);
			status = string.Format("{0} - {1}",PaymentMessages.AcquireFailed,updateSettleMentStatusResponse.ErrorMessage);
			return false;
		}

		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			SecureTradingTransactionQueryXmlResponse transactionQueryResponse = _secureTradingXmlRequester.TransactionQuery(payment.TransactionId, payment.PaymentMethod);
			
			if (PaymentQualifiesForCancel(transactionQueryResponse)) 
				return CancelPaymentInternal(payment, out status);

			Guard.Against.PaymentStatusIsNotSettled(transactionQueryResponse);

			SecureTradingRefundXmlResponse refundXmlResponse = _secureTradingXmlRequester.Refund(payment.TransactionId,
				payment.PurchaseOrder.OrderGuid.ToString(), payment.PaymentMethod);

			if (refundXmlResponse.ErrorCode == SecureTradingErrorCode.Success)
			{
				status = PaymentMessages.RefundSuccess;
				payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Refunded);
				return true;
			}

			_loggingService.Log<SecureTradingPaymentMethodService>(string.Format("failed to refund payment. Message: {0} Response was:\r\n {1}", refundXmlResponse.ErrorMessage, refundXmlResponse.XmlResponse));
			status = string.Format("{0} - {1}", PaymentMessages.RefundFailed, refundXmlResponse.ErrorMessage);
			payment.PaymentStatus = _paymentStatusRepository.Get((int) PaymentStatusCode.Declined); //TODO: should payment status be changed in case request was declined / failed?
			return false;
		}

		/// <summary>
		/// Payment may in some cases qualify for cancelation instead of refund as acquiering money takes one day to process. 
		/// </summary>
		/// <param name="transactionQueryResponse"></param>
		/// <returns></returns>
		private bool PaymentQualifiesForCancel(SecureTradingTransactionQueryXmlResponse transactionQueryResponse)
		{
			return
			( 
				transactionQueryResponse.SettleStatus == SecureTradingSettlementStatus.PendingSettlement ||
			    transactionQueryResponse.SettleStatus == SecureTradingSettlementStatus.PendingSettlementManuallyOverridden ||
				transactionQueryResponse.SettleStatus == SecureTradingSettlementStatus.Suspended
			);
		}

		private string GetQueryStringParameter(NameValueCollection queryString, string key)
		{
			if (queryString[key] != null) return queryString[key];

			return string.Empty;
		}
	}
}

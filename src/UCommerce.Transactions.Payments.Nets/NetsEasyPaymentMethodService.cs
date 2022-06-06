using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Environment;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Transactions.Payments.Nets;

namespace Ucommerce.Transactions.Payments.Dibs
{
	/// <summary>
	/// Implementation of the Nets Easy payment provider.
	/// </summary>
	public class NetsEasyPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IWebRuntimeInspector _webRuntimeInspector;
	    private readonly ILoggingService _loggingService;
		private AbstractPageBuilder PageBuilder { get; set; }

		public NetsEasyPaymentMethodService(NetsPageBuilder pageBuilder, IWebRuntimeInspector webRuntimeInspector, ILoggingService loggingService)
		{
			_webRuntimeInspector = webRuntimeInspector;
		    _loggingService = loggingService;
			PageBuilder = pageBuilder;
		}

		/// <summary>
		/// Renders the page with the information needed by the payment provider.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>The html rendered.</returns>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		/// <summary>
		/// Processes the callback and excecutes a pipeline if there is one specified for this paymentmethodservice.
		/// </summary>
		/// <param name="payment">The payment to process.</param>
		public override void ProcessCallback(Payment payment)
		{
			Guard.Against.MissingHttpContext(_webRuntimeInspector);
			//Guard.Against.MissingRequestParameter("transact");
			Guard.Against.PaymentNotPendingAuthorization(payment);

			//string transactionId = GetTransactionParameter();

			var hashVeryfied = false; //VerifyMd5Hash(payment);

			//payment.TransactionId = transactionId;
			payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
			ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();
			
			string amount = payment.Amount.ToCents().ToString();
			var referenceId = payment.ReferenceId;
			var transactionId = payment.TransactionId;

            status = null;

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
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();

			var amount = payment.Amount.ToCents().ToString();

			var transactionId = payment.TransactionId;
			var referenceId = payment.ReferenceId;

			status = null;

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
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();

			var referenceId = payment.ReferenceId;

			status = null;

			return false;
		}
	}
}

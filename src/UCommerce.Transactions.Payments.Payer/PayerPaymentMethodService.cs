using System;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;

namespace Ucommerce.Transactions.Payments.Payer
{
	/// <summary>
	/// Implementation of the http://payer.se payment provider
	/// </summary>
	public class PayerPaymentMethodService : ExternalPaymentMethodService
	{
		private PayerPageBuilder PageBuilder { get; set; }
		private PayerMd5Computer Md5Computer { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PayerPaymentMethodService"/> class.
		/// </summary>
		public PayerPaymentMethodService(PayerPageBuilder pageBuilder, PayerMd5Computer md5Computer)
		{
			PageBuilder = pageBuilder;
			Md5Computer = md5Computer;
		}

		/// <summary>
		/// Renders the forms to be submitted to the payment provider.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>A string containing the html form.</returns>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		/// <summary>
		/// Processed the callback received from the payment provider.
		/// </summary>
		/// <param name="payment">The payment.</param>
		public override void ProcessCallback(Payment payment)
		{
			string key1 = payment.PaymentMethod.DynamicProperty<string>().Key1;
			string key2 = payment.PaymentMethod.DynamicProperty<string>().Key2;

			HttpRequest request = HttpContext.Current.Request;

			string payerCallbackType = request["payer_callback_type"];

			string referenceId = request["payer_merchant_reference_id"];
			var paymentRequest = new PaymentRequest(payment.PurchaseOrder, payment);
			if (MerchantReferenceDoNotMatch(payment, referenceId))
			{
				SetPageOutput(new PayerFalsePage(), paymentRequest);
				return;
			}

			if (!IsValidRequest(request, key1, key2))
			{
				SetPageOutput(new PayerFalsePage(), paymentRequest);
				return;
			}


			if (string.Equals("AUTH", payerCallbackType, StringComparison.InvariantCultureIgnoreCase) && payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.PendingAuthorization)
			{
				payment.Save();
				SetPageOutput(new PayerTruePage(), paymentRequest);
				return;
			}
			
			if (string.Equals("SETTLE", payerCallbackType, StringComparison.InvariantCultureIgnoreCase) && payment.PaymentStatus.PaymentStatusId == (int )PaymentStatusCode.PendingAuthorization)
			{
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
				payment.Save();
				SetPageOutput(new PayerTruePage(), paymentRequest);
			}
			else
			{
				SetPageOutput(new PayerFalsePage(), paymentRequest);
			}

			ProcessPaymentRequest(paymentRequest);
		}

		protected bool MerchantReferenceDoNotMatch(Payment payment, string referenceId)
		{
			return string.IsNullOrEmpty(referenceId) || payment.ReferenceId != referenceId;
		}

		public void SetPageOutput(AbstractPageBuilder builder, PaymentRequest paymentRequest)
		{
			var page = builder.Build(paymentRequest);
			HttpContext.Current.Response.Write(page);
		}

		/// <summary>
		/// Determines whether the request is valid
		/// </summary>
		/// <param name="request">The request</param>
		/// <param name="key1">Key1 from configuration</param>
		/// <param name="key2">Key2 from configuration</param>
		/// <returns>
		/// 	<c>true</c> if the request is valid, otherwise <c>false</c>.
		/// </returns>
		protected bool IsValidRequest(HttpRequest request, string key1, string key2)
		{
			var computer = Md5Computer;
			var urlWithoutMd5Sum = request.Url.AbsoluteUri.Substring(0, (request.Url.AbsoluteUri.IndexOf("&md5sum", 0) + 1) - 1);
			var md5SumParameter = request["md5sum"];
			var createdMd5 = computer.GetMd5Key(urlWithoutMd5Sum, key1, key2);
			return string.Equals(md5SumParameter, createdMd5, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			status = PaymentMessages.AcquireNotAutomatic;
			return true;
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			status = PaymentMessages.RefundNotAutomatic;
			return true;
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			status = PaymentMessages.CancelNotAutomatic;
			return true;
		}
	}
}
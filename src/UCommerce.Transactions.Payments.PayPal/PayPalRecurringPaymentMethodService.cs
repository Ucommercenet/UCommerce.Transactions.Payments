using System;
using System.Security;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure.Logging;

namespace Ucommerce.Transactions.Payments.PayPal
{
	/// <summary>
	/// Support for recurring payments with PayPal.
	/// </summary>
	/// <remarks>
	/// Main difference lies in added parameters sent during auth and no support for Capture, Void, Refund.
	/// </remarks>
	public class PayPalRecurringPaymentMethodService : PayPalPaymentMethodService
	{
		public PayPalRecurringPaymentMethodService(
			PayPalWebSitePaymentsStandardRecurringPaymentPageBuilder pageBuilder, ILoggingService loggingService) : base(pageBuilder, loggingService) {}

		public override Payment AcquirePayment(Payment paymentToAcquire)
		{
			throw new NotSupportedException(
				"PayPal Recurring payment method service does not support acquire. PayPal handles acquiring automatically until subscription is cancelled or reaches end of subscription interval");
		}

		public override void ProcessCallback(Payment payment)
		{
			var request = HttpContext.Current.Request;

			if (!IsValidCallback(payment.PaymentMethod))
			{
				string message = string.Format("Could not validate IPN from PayPal. TransactionId {0}. Request received {1} at {2}.", request["txn_id"], request.Form, request.RawUrl);
				LoggingService.Debug<PayPalRecurringPaymentMethodService>(message);
				throw new SecurityException(message);
			}

			string transactParameter = request["subscr_id"];
			if (string.IsNullOrEmpty(transactParameter))
				throw new ArgumentException(@"subscr_id must be present in query string.");

			payment.TransactionId = transactParameter;

			bool isPendingAuth = payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.PendingAuthorization;
			if (isPendingAuth)
			{
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}

			payment.Save();
		}

		public override Payment CreatePayment(PaymentRequest request)
		{
			var recurringRequest = ThrowInvalidOperationExceptionIfNotARecurringPaymentRequest(request);
			ValidateRecurrencePerPayPalRules(recurringRequest);

			var payment = base.CreatePayment(request);

			// Add the recurring variables for PayPal
			payment["src"] = recurringRequest.Recurs ? "1" : "0";

			// Subscription recurs this many times, int > 1. Only valid if src = 1
			if (recurringRequest.Recurs && recurringRequest.RecurLimit.HasValue && recurringRequest.RecurLimit.Value > 1)
				payment["srt"] = recurringRequest.RecurLimit.Value.ToString(); 

			// Subscription duration, only ints
			payment["p3"] = recurringRequest.DurationBetweenEachRecurrence.ToString();
			
			// Subscription units, allowed values D(ays), W(eek), M(onth), Y(ear)
			payment["t3"] = MapRecurrencyUnitToPayPalUnit(recurringRequest.DurationUnit);
			
			payment.Save();
			
			return payment;
		}

		private void ValidateRecurrencePerPayPalRules(RecurringPaymentRequest recurringRequest)
		{
			if (recurringRequest.DurationBetweenEachRecurrence < 1)
				throw new NotSupportedException("Duration between each recurrence must be greater than 1 regardless of the duration unit. Please set DurationBetweenEachRecurrence accordingly on your recurring payment request.");

			switch (recurringRequest.DurationUnit)
			{
				case DurationUnit.Day:
					if (recurringRequest.DurationBetweenEachRecurrence > 90 ) throw new NotSupportedException("Duration between each recurrence must be between 1 and 90 days.");
					break;
				case DurationUnit.Week:
					if (recurringRequest.DurationBetweenEachRecurrence > 52) throw new NotSupportedException("Duration between each recurrence must be between 1 and 52 weeks.");
					break;
				case DurationUnit.Month:
					if (recurringRequest.DurationBetweenEachRecurrence > 24) throw new NotSupportedException("Duration between each recurrence must be between 1 and 24 months.");
					break;
				case DurationUnit.Year:
					if (recurringRequest.DurationBetweenEachRecurrence > 5) throw new NotSupportedException("Duration between each recurrence must be between 1 and 5 years.");
					break;
			}
		}

		private string MapRecurrencyUnitToPayPalUnit(DurationUnit durationUnit)
		{
			switch (durationUnit)
			{
				case DurationUnit.Year:
					return "Y";
				case DurationUnit.Week:
					return "W";
				case DurationUnit.Month:
					return "M";
				case DurationUnit.Day:
					return "D";
				default:
					throw new NotSupportedException("Duration unit not supported. Valid options are Year, Month, Week, Day.");
			}
		}

		private RecurringPaymentRequest ThrowInvalidOperationExceptionIfNotARecurringPaymentRequest(PaymentRequest request)
		{
			var recurringRequest = request as RecurringPaymentRequest;
			if (recurringRequest == null) throw new InvalidOperationException("Recurring payment request required for the PayPal Recurring Payment Method Service. Please ensure that you use a RecurringPaymentRequest with this provider.");

			return recurringRequest;
		}
	}
}

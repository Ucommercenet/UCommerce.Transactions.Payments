using System;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Infrastructure;
using UCommerce.Infrastructure.Environment;

namespace UCommerce.Transactions.Payments.PayEx
{
	public static class PayExGuardExtensions
	{
		public static void PaymentNotPendingAuthorization(this Guard guard, Payment payment)
		{
			if (payment.PaymentStatus.PaymentStatusId != (int) PaymentStatusCode.PendingAuthorization)
				throw new InvalidOperationException(string.Format("Payment {0} does not have payment status 'pending authorization'.", payment.PaymentId));
		}

		public static void NoHttpContext(this Guard guard, IWebRuntimeInspector webRuntimeInspector)
		{
			if (!webRuntimeInspector.IsWebContext())
			{
				throw new InvalidOperationException("PayEx payment method service ProccessCallback cannot be used outside HttpContext");
			}
		}

		public static void MissingParameterInResponse(this Guard guard, string key)
		{
			if (HttpContext.Current == null)
			{
				if (string.IsNullOrEmpty(key))
					throw new ArgumentException(string.Format(@"{0} must be present in request and cannot be an empty.",key));
			}
		}

		public static void ResponseWasNotOk(this Guard guard, PayExXmlMessage message, Payment payment)
		{
			if (!message.StatusCode)
				throw new ArgumentException(string.Format("Error when processing callback for payment {1}: {0}.", message.ErrorDescription, payment.PaymentId));
		}

		public static void PaymentAlreadyCompleted(this Guard guard, PayExXmlMessage message, Payment payment)
		{
			if (message.AlreadyCompleted)
				throw new InvalidOperationException(string.Format("The payment {0} has already been completed.", payment.PaymentId));
		}
	}
}

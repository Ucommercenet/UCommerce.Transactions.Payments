using System;
using System.Security;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure;

namespace Ucommerce.Transactions.Payments.Adyen
{
	public static class AdyenGuardExtensions
	{
		public static void NotPendingAuthorizationStatus(this Guard @guard, Payment payment)
		{
			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
				throw new InvalidOperationException(string.Format(
					"Payment {0} does not have payment status 'pending authorization'.", payment.PaymentId));
		}

		public static void MessageNotAuthenticated(this Guard @guard, bool messageAuthenticated)
		{
			if (!messageAuthenticated)
			{
				throw new SecurityException(
					"The signature for the request is not approved. Make sure that data from Adyen is not modified.");
			}
		}
	}
}

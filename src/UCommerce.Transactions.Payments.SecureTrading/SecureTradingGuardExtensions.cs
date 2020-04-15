using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure;

namespace Ucommerce.Transactions.Payments.SecureTrading
{
	public static class SecureTradingGuardExtensions
	{
		public static void TransactionQueryRequestDoesNotMatchesDeclinedStatus(this Guard guard, SecureTradingTransactionQueryXmlResponse transactionQueryRequest)
		{
			if (transactionQueryRequest.SettleStatus != SecureTradingSettlementStatus.Cancelled)
			{
				throw new InvalidOperationException("QueryString parameter and transactionrequest did not match.");
			}
		}

		public static void TransactionQueryRequestDoesNotMatchesAuthStatus(this Guard guard, SecureTradingTransactionQueryXmlResponse transactionQueryRequest)
		{
			if (!transactionQueryRequest.Success)
			{
				throw new InvalidOperationException("QueryString parameter and transactionrequest did not match.");
			}
		}

		public static void TransactionRequestDoesNotMatchesOrder(this Guard guard, Payment payment, SecureTradingTransactionQueryXmlResponse transactionQueryRequest)
		{
			if (transactionQueryRequest.OrderReference != payment.PurchaseOrder.OrderGuid.ToString())
			{
				throw new InvalidOperationException(
					string.Format("Transactionquery orderreference: {0} did not match orderrefererence: {1}",
						transactionQueryRequest.OrderReference, payment.PurchaseOrder.OrderGuid));
			}
		}

		public static void NotPendingAuthorizationForPayment(this Guard guard, Payment payment, IRepository<PaymentStatus> paymentStatusRepository)
		{
			if (payment.PaymentStatus != paymentStatusRepository.Get((int) PaymentStatusCode.PendingAuthorization))
			{
				throw new InvalidOperationException("Payment wasn't pending authorization for auth request. It was: " + payment.PaymentStatus.Name);
			}
		}

		public static void NullOrEmptyString(this Guard guard, string value,string errorMessage)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidOperationException(errorMessage);
			}
		}

		public static void PaymentDoesNotQualifyForCancellation(this Guard guard,
			SecureTradingTransactionQueryXmlResponse transactionQueryResponse)
		{
			if (transactionQueryResponse.SettleStatus != SecureTradingSettlementStatus.PendingSettlement &&
			    transactionQueryResponse.SettleStatus != SecureTradingSettlementStatus.PendingSettlementManuallyOverridden &&
			    transactionQueryResponse.SettleStatus != SecureTradingSettlementStatus.Suspended)
			{
				throw new InvalidOperationException(string.Format("Status was: {0}. Expected PendingSettlement,PendingSettlementManuallyOverridden or Suspended.", transactionQueryResponse.SettleStatus.ToString()));					
			}
		}

		public static void PaymentStatusIsNotSuspended(this Guard guard, SecureTradingTransactionQueryXmlResponse response)
		{
			if (response.SettleStatus != SecureTradingSettlementStatus.Suspended)
			{
				throw new InvalidOperationException(string.Format("Cannot acquire payment. Status from payment was not suspended. Status was: {0}", response.SettleStatus.ToString()));
			}
		}

		public static void PaymentStatusIsNotSettled(this Guard guard, SecureTradingTransactionQueryXmlResponse response)
		{
			if (response.SettleStatus != SecureTradingSettlementStatus.Settled)
			{
				throw new InvalidOperationException("cannot refund a payment that is not acquired.");
			}
		}
	}
}

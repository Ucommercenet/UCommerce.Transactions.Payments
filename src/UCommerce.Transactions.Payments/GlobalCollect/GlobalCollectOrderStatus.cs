using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public enum GlobalCollectOrderStatus
	{
		OrderCreated = 0,
		RefundCreated = 5,
		OrderWithAttempt = 10,
		RefundFailed = 15,
		OrderWithSuccessfulAttempt = 20,
		OrderSuccessful = 40,
		RefundSuccessful = 45,
		OrderOpen = 60,
		EndedByMerchant = 90,
		EndedAutomatically = 91,
		RejectedByMerchant = 98,
		CancelledByMerchant = 99
	}
}

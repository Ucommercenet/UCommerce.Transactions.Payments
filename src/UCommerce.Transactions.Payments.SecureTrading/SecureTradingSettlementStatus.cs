using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ucommerce.Transactions.Payments.SecureTrading
{
	public enum SecureTradingSettlementStatus
	{
		PendingSettlement = 0,
		PendingSettlementManuallyOverridden = 1,
		Suspended = 2,
		Cancelled = 3,
		Settled = 100
	}
}

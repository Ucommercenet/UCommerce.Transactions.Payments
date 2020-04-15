using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ucommerce.Transactions.Payments.SagePay
{
	public enum SagePayStatusCode
	{
		Ok,
		Malformed,
		Invalid,
		Unknown,
		Error,
		Abort,
		Registered,
		Rejected,
		Authenticated
	}
}

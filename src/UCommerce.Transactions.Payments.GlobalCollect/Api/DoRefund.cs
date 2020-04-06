using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api
{
	public class DoRefund : BasicRequest
	{
		public DoRefund()
			: base("DO_REFUND")
		{
			Payment = new ApiPayment();
			Params.Parameters.Add(Payment);
		}

		public ApiPayment Payment { get; private set; }
	}
}

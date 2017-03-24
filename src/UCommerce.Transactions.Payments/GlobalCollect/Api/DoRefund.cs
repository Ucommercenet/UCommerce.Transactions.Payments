using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
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

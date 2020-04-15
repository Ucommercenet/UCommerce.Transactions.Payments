using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api
{
	public class SetPayment : BasicRequest
	{
		public SetPayment()
			: base("SET_PAYMENT")
		{
			Payment = new ApiPayment();
			Params.Parameters.Add(Payment);
		}

		public ApiPayment Payment { get; private set; }
	}
}

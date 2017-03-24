using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
{
	public class GetOrder : BasicRequest
	{
		public GetOrder()
			: base("GET_ORDER")
		{
			Order = new ApiOrder();
			Params.Parameters.Add(Order);
		}

		public ApiOrder Order { get; private set; }
	}
}

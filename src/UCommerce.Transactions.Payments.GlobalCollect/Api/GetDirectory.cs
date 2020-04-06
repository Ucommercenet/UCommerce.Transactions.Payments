using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api
{
	public class GetDirectory: BasicRequest
	{
		public GetDirectory()
			: base("GET_DIRECTORY")
		{
			General = new General();
			Params.Parameters.Add(General);
		}

		public General General { get; private set; }
	}
}

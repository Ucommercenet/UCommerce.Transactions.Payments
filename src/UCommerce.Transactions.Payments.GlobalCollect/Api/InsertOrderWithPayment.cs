using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
{
	public class InsertOrderWithPayment : BasicRequest
	{
		public InsertOrderWithPayment()
			: base("INSERT_ORDERWITHPAYMENT")
		{
			Order = new ApiOrder();
			Payment = new ApiPayment();

			Params.Parameters.Add(Order);
			Params.Parameters.Add(Payment);
		}

		public ApiOrder Order { get; private set; }

		public ApiPayment Payment { get; private set; }

		public InsertOrderWithPaymentResponse Response { get; private set; }
		
		public override void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			base.FromModifiedXml(doc, path);

			if (doc.Exists("XML/REQUEST/RESPONSE"))
			{
				Response = new InsertOrderWithPaymentResponse();
				Response.FromModifiedXml(doc, "XML/REQUEST/RESPONSE");
			}
		}
	}
}

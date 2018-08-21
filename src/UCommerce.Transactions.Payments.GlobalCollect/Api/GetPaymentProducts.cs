using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
{
	public class GetPaymentProducts : BasicRequest
	{
		public GetPaymentProducts()
			: base("GET_PAYMENTPRODUCTS")
		{
			General = new General();
			Params.Parameters.Add(General);
		}

		public General General { get; private set; }

		public GetPaymentProductsResponse Response { get; private set; }

		public override void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			base.FromModifiedXml(doc, path);

			if (doc.Exists("XML/REQUEST/RESPONSE"))
			{
				Response = new GetPaymentProductsResponse();
				Response.FromModifiedXml(doc, "XML/REQUEST/RESPONSE");
			}
		}
	}
}

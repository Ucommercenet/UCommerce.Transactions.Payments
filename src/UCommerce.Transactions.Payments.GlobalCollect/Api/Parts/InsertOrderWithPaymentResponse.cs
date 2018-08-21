using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class InsertOrderWithPaymentResponse
	{
		public string Result { get; set; }

		public ResponseMeta Meta { get; set; }

		public IList<PaymentData> PaymentRows { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Result = doc.GetStringFromXml(path + "/RESULT");
			Meta = new ResponseMeta();
			Meta.FromModifiedXml(doc, path);
			PaymentRows = new List<PaymentData>();

			var nodes = doc.GetNodes(path + "/ROW");
			foreach (var node in nodes)
			{
				var data = new PaymentData();
				data.FromModifiedXml(node, string.Empty);
				PaymentRows.Add(data);
			}
		}
	}
}

using System.Collections.Generic;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class GetPaymentProductsResponse : IApiDataPartReadOnly
	{
		public string Result { get; set; }

		public ResponseMeta Meta { get; set; }

		public IList<PaymentProductData> PaymentProducts { get; private set; }
		
		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Result = doc.GetStringFromXml(path + "/RESULT");
			Meta = new ResponseMeta();
			Meta.FromModifiedXml(doc, path);
			PaymentProducts = new List<PaymentProductData>();

			var nodes = doc.GetNodes(path + "/ROW");
			foreach (var node in nodes)
			{
				var data = new PaymentProductData();
				data.FromModifiedXml(node, string.Empty);
				PaymentProducts.Add(data);
			}
		}
	}
}

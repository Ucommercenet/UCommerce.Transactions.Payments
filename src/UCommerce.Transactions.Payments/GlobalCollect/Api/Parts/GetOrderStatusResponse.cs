namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class GetOrderStatusResponse : IApiDataPartReadOnly
	{
		public string Result { get; set; }

		public ResponseMeta Meta { get; set; }

		public OrderStatus Status { get; set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Result = doc.GetStringFromXml(path + "/RESULT");
			Meta = new ResponseMeta();
			Meta.FromModifiedXml(doc, path);
			Status = new OrderStatus();

			Status.FromModifiedXml(doc, path + "/STATUS");
		}
	}
}

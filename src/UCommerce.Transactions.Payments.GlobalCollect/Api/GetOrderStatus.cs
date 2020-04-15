using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api
{
	public class GetOrderStatus : BasicRequest
	{
		public GetOrderStatus() : base("GET_ORDERSTATUS")
		{
			Order = new ApiOrder();
			Params.Parameters.Add(Order);
		}

		public ApiOrder Order { get; private set; }

		public GetOrderStatusResponse Response { get; private set; }

		public override void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			base.FromModifiedXml(doc, path);

			if (doc.Exists("XML/REQUEST/RESPONSE"))
			{
				Response = new GetOrderStatusResponse();
				Response.FromModifiedXml(doc, "XML/REQUEST/RESPONSE");
			}
		}
	}
}

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api
{
	public interface IApiDataPartReadOnly
	{
		void FromModifiedXml(ModifiedXmlDocument doc, string path);
	}
}
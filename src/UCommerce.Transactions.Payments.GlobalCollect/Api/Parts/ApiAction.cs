using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class ApiAction : IApiDataPart
	{
		public ApiAction(string name)
		{
			Name = name;
		}

		public ApiAction(ModifiedXmlDocument doc, string path)
		{
			FromModifiedXml(doc, path);
		}

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Name = doc.GetStringFromXml(path + "/ACTION");
		}

		public string Name { get; set; }

		public void AddToStringBuilder(StringBuilder sb)
		{
			PartsHelper.AddString(sb, Name, "ACTION");
		}
	}
}

using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class Meta : IApiDataPart
	{
		public Meta() {}

		public Meta(ModifiedXmlDocument doc, string path)
		{
			FromModifiedXml(doc, path);
		}

		public long MerchantId { get; set; }

		public string IpAddress { get; set; }

		public string Version { get; set; }

		public void AddToStringBuilder(StringBuilder sb)
		{
			sb.Append("<META>");

			PartsHelper.AddLong(sb, MerchantId, "MERCHANTID");
			PartsHelper.AddString(sb, IpAddress, "IPADDRESS", 32);
			PartsHelper.AddString(sb, Version, "VERSION", 10);

			sb.Append("</META>");
		}

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			MerchantId = doc.GetLongFromXml(path + "/META/MERCHANTID");
			IpAddress = doc.TryGetStringFromXml(path + "/META/IPADDRESS");
			Version = doc.TryGetStringFromXml(path + "/META/VERSION");
		}
	}
}

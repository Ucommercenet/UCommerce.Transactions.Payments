using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class General : IApiDataPart
	{
		public General() {}

		public General(ModifiedXmlDocument doc, string path)
		{
			FromModifiedXml(doc, path);
		}

		public long? PaymentProductId { get; set; }

		public string CountryCode { get; set; }

		public string CurrencyCode { get; set; }

		public string LanguageCode { get; set; }

		public void AddToStringBuilder(StringBuilder sb)
		{
			sb.Append("<GENERAL>");
	
			PartsHelper.AddNullableLong(sb, PaymentProductId, "PAYMENTPRODUCTID");
			PartsHelper.AddString(sb, CountryCode, "COUNTRYCODE", 2);
			PartsHelper.AddString(sb, CurrencyCode, "CURRENCYCODE", 3);
			PartsHelper.AddString(sb, LanguageCode, "LANGUAGECODE", 2);

			sb.Append("</GENERAL>");
		}

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			PaymentProductId = doc.GetNullableLongFromXml(path + "/GENERAL/PAYMENTPRODUCTID");
			CountryCode = doc.TryGetStringFromXml(path + "/GENERAL/COUNTRYCODE");
			CurrencyCode = doc.TryGetStringFromXml(path + "/GENERAL/CURRENCYCODE");
			LanguageCode = doc.TryGetStringFromXml(path + "/GENERAL/LANGUAGECODE");
		}
	}
}

using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class ApiOrder : IApiDataPart, IAddExtraData
	{
		private readonly ExtraData _extraData = new ExtraData();

		public ApiOrder() { }

		public ApiOrder(ModifiedXmlDocument doc, string path)
		{
			FromModifiedXml(doc, path);
		}

		public long? OrderId { get; set; }

		public long? MerchantOrderId { get; set; }

		public long? Amount { get; set; }

		public string CurrencyCode { get; set; }

		public string LanguageCode { get; set; }

		public string CountryCode { get; set; }

		public string MerchantReference { get; set; }

		//Address fields
		public Address BillingAddress { get; set; }

		public Address ShippingAddress { get; set; }

		public void AddToStringBuilder(StringBuilder sb)
		{
			sb.Append("<ORDER>");
	
			PartsHelper.AddNullableLong(sb, OrderId, "ORDERID");
			PartsHelper.AddNullableLong(sb, MerchantOrderId, "MERCHANTORDERID");
			PartsHelper.AddNullableLong(sb, Amount, "AMOUNT");
			PartsHelper.AddString(sb, CurrencyCode, "CURRENCYCODE", 3);
			PartsHelper.AddString(sb, LanguageCode, "LANGUAGECODE", 2);
			PartsHelper.AddString(sb, CountryCode, "COUNTRYCODE", 2);
			PartsHelper.AddString(sb, MerchantReference, "MERCHANTREFERENCE");

			if (BillingAddress != null)
			{
				PartsHelper.AddString(sb, BillingAddress.FirstName, "FIRSTNAME", 15);
				PartsHelper.AddString(sb, BillingAddress.LastName, "SURNAME", 35);
				PartsHelper.AddString(sb, BillingAddress.PhoneNumber, "PHONENUMBER", 20);
				PartsHelper.AddString(sb, BillingAddress.StreetLine1, "STREET", 50);
				PartsHelper.AddString(sb, BillingAddress.StreetLine2, "ADDITIONALADDRESSINFO", 50);
				PartsHelper.AddString(sb, BillingAddress.City, "CITY", 40);
				PartsHelper.AddString(sb, BillingAddress.IpAddress, "IPADDRESSCUSTOMER", 32);
				PartsHelper.AddString(sb, BillingAddress.Email, "EMAIL", 70);
				PartsHelper.AddString(sb, BillingAddress.Zip, "ZIP", 9);
				PartsHelper.AddString(sb, BillingAddress.State, "STATE", 35);
				PartsHelper.AddString(sb, BillingAddress.CompanyName, "COMPANYNAME", 40);
			}

			if (ShippingAddress != null)
			{
				PartsHelper.AddString(sb, ShippingAddress.FirstName, "SHIPPINGFIRSTNAME", 15);
				PartsHelper.AddString(sb, ShippingAddress.LastName, "SHIPPINGSURNAME", 35);
				PartsHelper.AddString(sb, ShippingAddress.StreetLine1, "SHIPPINGSTREET", 50);
				PartsHelper.AddString(sb, ShippingAddress.StreetLine2, "SHIPPINGADDITIONALADDRESSINFO", 50);
				PartsHelper.AddString(sb, ShippingAddress.City, "SHIPPINGCITY", 40);
				PartsHelper.AddString(sb, ShippingAddress.CountryCode, "SHIPPINGCOUNTRYCODE", 2);
				PartsHelper.AddString(sb, ShippingAddress.Zip, "SHIPPINGZIP", 10);
				PartsHelper.AddString(sb, ShippingAddress.State, "SHIPPINGSTATE", 35);
				PartsHelper.AddString(sb, ShippingAddress.CompanyName, "SHIPPINGCOMPANYNAME", 40); // ?? This is not found in the specifications. :-S
			}

			_extraData.AddToStringBuilder(sb);

			sb.Append("</ORDER>");
		}

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			OrderId = doc.GetNullableLongFromXml(path + "/ORDER/ORDERID");
			MerchantOrderId = doc.GetNullableLongFromXml(path + "/ORDER/MERCHANTORDERID");
			Amount = doc.GetNullableLongFromXml(path + "/ORDER/AMOUNT");
			CurrencyCode = doc.TryGetStringFromXml(path + "/ORDER/CURRENCYCODE");
			LanguageCode = doc.TryGetStringFromXml(path + "/ORDER/LANGUAGECODE");
			CountryCode = doc.TryGetStringFromXml(path + "/ORDER/COUNTRYCODE");
			MerchantReference = doc.TryGetStringFromXml(path + "/ORDER/MERCHANTREFERENCE");

			BillingAddress = new Address()
			{
				FirstName = doc.TryGetStringFromXml(path + "/ORDER/FIRSTNAME"),
				LastName = doc.TryGetStringFromXml(path + "/ORDER/SURNAME"),
				PhoneNumber = doc.TryGetStringFromXml(path + "/ORDER/PHONENUMBER"),
				StreetLine1 = doc.TryGetStringFromXml(path + "/ORDER/STREET"),
				StreetLine2 = doc.TryGetStringFromXml(path + "/ORDER/ADDITIONALADDRESSINFO"),
				City = doc.TryGetStringFromXml(path + "/ORDER/CITY"),
				Zip = doc.TryGetStringFromXml(path + "/ORDER/ZIP"),
				State = doc.TryGetStringFromXml(path + "/ORDER/STATE"),
				CompanyName = doc.TryGetStringFromXml(path + "/ORDER/COMPANYNAME")
			};

			ShippingAddress = new Address()
			{
				FirstName = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGFIRSTNAME"),
				LastName = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGSURNAME"),
				StreetLine1 = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGSTREET"),
				StreetLine2 = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGADDITIONALADDRESSINFO"),
				City = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGCITY"),
				Zip = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGZIP"),
				State = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGSTATE"),
				CompanyName = doc.TryGetStringFromXml(path + "/ORDER/SHIPPINGCOMPANYNAME")
			};
		}

		public void AddExtraData(string s, string tag)
		{
			_extraData.AddExtraData(s, tag);
		}
	}
}

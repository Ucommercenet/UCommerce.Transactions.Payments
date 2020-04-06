using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class ApiPayment : IApiDataPart, IAddExtraData
	{
		private ExtraData _extraData = new ExtraData();

		public ApiPayment() {}

		public ApiPayment(ModifiedXmlDocument doc, string path)
		{
			FromModifiedXml(doc, path);
		}

		public string ProfileToken { get; set; }

		public int? PaymentProductId { get; set; }

		public long? Amount { get; set; }

		public string CurrencyCode { get; set; }

		public string LanguageCode { get; set; }

		public string CountryCode { get; set; }

		public string ReturnUrl { get; set; }

		public int? DateCollect { get; set; }

		public long? OrderId { get; set; }

		public int? AttemptId { get; set; }

		public int? EffortId { get; set; }

		public string MerchantReference { get; set; }

		public bool UseAuthenticationIndicator { get; set; }

		public void AddToStringBuilder(StringBuilder sb)
		{
			sb.Append("<PAYMENT>");

			PartsHelper.AddString(sb, ProfileToken, "PROFILETOKEN", 40);
			PartsHelper.AddNullableLong(sb, PaymentProductId, "PAYMENTPRODUCTID");
			PartsHelper.AddNullableLong(sb, Amount, "AMOUNT");
			PartsHelper.AddString(sb, CurrencyCode, "CURRENCYCODE", 3);
			PartsHelper.AddString(sb, LanguageCode, "LANGUAGECODE", 2);
			PartsHelper.AddString(sb, CountryCode, "COUNTRYCODE", 2);
			PartsHelper.AddString(sb, ReturnUrl, "RETURNURL", 512);
			PartsHelper.AddNullableInt(sb, DateCollect, "DATECOLLECT");
			PartsHelper.AddNullableLong(sb, OrderId, "ORDERID");
			PartsHelper.AddNullableInt(sb, EffortId, "EFFORTID");
			PartsHelper.AddNullableInt(sb, AttemptId, "ATTEMPTID");
			PartsHelper.AddString(sb, MerchantReference, "MERCHANTREFERENCE", 30);

			if (UseAuthenticationIndicator)
			{
				PartsHelper.AddString(sb, "1", "AUTHENTICATIONINDICATOR");
			}

			_extraData.AddToStringBuilder(sb);

			sb.Append("</PAYMENT>");
		}

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			ProfileToken = doc.TryGetStringFromXml(path + "/PAYMENT/PROFILETOKEN");
			PaymentProductId = doc.GetNullableIntFromXml(path + "/PAYMENT/PAYMENTPRODUCTID");
			Amount = doc.GetNullableLongFromXml(path + "/PAYMENT/AMOUNT");
			CurrencyCode = doc.TryGetStringFromXml(path + "/PAYMENT/CURRENCYCODE");
			LanguageCode = doc.TryGetStringFromXml(path + "/PAYMENT/LANGUAGECODE");
			CountryCode = doc.TryGetStringFromXml(path + "/PAYMENT/COUNTRYCODE");
			ReturnUrl = doc.TryGetStringFromXml(path + "/PAYMENT/RETURNURL");
			DateCollect = doc.GetNullableIntFromXml(path + "/PAYMENT/DATECOLLECT");
			OrderId = doc.GetNullableLongFromXml(path + "/PAYMENT/ORDERID");
			EffortId = doc.GetNullableIntFromXml(path + "/PAYMENT/EFFORTID");
			AttemptId = doc.GetNullableIntFromXml(path + "/PAYMENT/ATTEMPTID");
			MerchantReference = doc.TryGetStringFromXml(path + "/PAYMENT/MERCHANTREFERENCE");
		}

		public void AddExtraData(string s, string tag)
		{
			_extraData.AddExtraData(s, tag);
		}
	}
}

using System;
using System.Collections.Generic;

namespace Ucommerce.Transactions.Payments.Adyen
{
	public enum AuthorizationResult
	{
		Authorised,
		Refused,
		Cancelled,
		Pending,
		Error
	}

	public class AuthorizationResultMessageData
	{
		public AuthorizationResult AuthorizationResult { get; set; }
		public string PspReference { get; set; }
		public string MerchantReference { get; set; }
		public string SkinCode { get; set; }
		public string MerchantSignature { get; set; }
		public string PaymentMethod { get; set; }
		public string ShopperLocale { get; set; }
		public string MerchantReturnData { get; set; }

		public void ExtractDataFromRequest(IDictionary<string, string> dict)
		{
			AuthorizationResult = BuildAuthorizationResult(GetValue(dict, "authResult"));
			PspReference = GetValue(dict, "pspReference");
			MerchantReference = GetValue(dict, "merchantReference");
			SkinCode = GetValue(dict, "skinCode");
			MerchantSignature = GetValue(dict, "merchantSig");
			PaymentMethod = GetValue(dict, "paymentMethod");
			ShopperLocale = GetValue(dict, "shopperLocale");
			MerchantReturnData = GetValue(dict, "merchantReturnData");
		}

		private AuthorizationResult BuildAuthorizationResult(string s)
		{
			s = s.Trim();
			switch (s)
			{
				case "AUTHORISED": return AuthorizationResult.Authorised;
				case "CANCELLED": return AuthorizationResult.Cancelled;
				case "ERROR": return AuthorizationResult.Error;
				case "PENDING": return AuthorizationResult.Pending;
				case "REFUSED": return AuthorizationResult.Refused;
				default: throw new NotSupportedException(string.Format("'{0}' is not a supported Payment AuthenticationResult.", s));
			}
		}

		private string GetValue(IDictionary<string, string> dict, string key)
		{
			if (dict.ContainsKey(key)) return dict[key];
			return string.Empty;
		}
	}
}

using System;
using System.Security.Cryptography;
using System.Text;

namespace UCommerce.Transactions.Payments.Adyen
{
	public class HmacCalculator
	{
		private string SharedSecret { get; set; }
		private UTF8Encoding Encoding { get; set; }

		public HmacCalculator(string sharedSecret)
		{
			if (string.IsNullOrEmpty(sharedSecret)) { throw new ArgumentException("sharedSecret"); }

			SharedSecret = sharedSecret;
			Encoding = new UTF8Encoding();
		}

		public string Execute(string signingString)
		{
			if (string.IsNullOrEmpty(signingString)) { throw new ArgumentException("signingString"); }

			var hmac = new HMACSHA1(Encoding.GetBytes(SharedSecret));
			var mac = Convert.ToBase64String(hmac.ComputeHash(Encoding.GetBytes(signingString)));
			hmac.Clear();

			return mac;
		}
	}
}

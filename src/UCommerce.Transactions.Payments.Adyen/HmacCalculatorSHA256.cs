using System;
using System.Security.Cryptography;
using System.Text;

namespace Ucommerce.Transactions.Payments.Adyen
{
	public class HmacCalculatorSHA256
	{
		public HmacCalculatorSHA256(string sharedSecret)
		{
			if (string.IsNullOrEmpty(sharedSecret))
			{
				throw new ArgumentException("sharedSecret");
			}

			SharedSecret = sharedSecret;
		}

		private string SharedSecret { get; set; }

		public string Execute(string signingString)
		{
			byte[] key = PackH(SharedSecret);
			byte[] data = Encoding.UTF8.GetBytes(signingString);

			try
			{
				using (HMACSHA256 hmac = new HMACSHA256(key))
				{
					// Compute the hmac on input data bytes
					byte[] rawHmac = hmac.ComputeHash(data);

					// Base64-encode the hmac
					return Convert.ToBase64String(rawHmac);
				}
			}
			catch (Exception e)
			{
				throw new Exception("Failed to generate HMAC : " + e.Message);
			}
		}

		private byte[] PackH(string hex)
		{
			if ((hex.Length % 2) == 1)
			{
				hex = '0' + hex;
			}

			byte[] bytes = new byte[hex.Length / 2];
			for (int i = 0; i < hex.Length; i += 2)
			{
				bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			}

			return bytes;
		}
	}
}

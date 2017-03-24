using System.Security.Cryptography;
using System.Text;

namespace UCommerce.Transactions.Payments.Ogone
{

	/// <summary>
	/// OgoneSha1Computer helps <see cref="OgonePaymentMethodService"/> make the sha sign by
	/// hashing a seed string.
	/// </summary>
	/// <remarks></remarks>
	public class OgoneSha1Computer
	{
		/// <summary>
		/// Computes the sha-1 hash.
		/// </summary>
		/// <param name="stringToHash">The string to hash.</param>
		/// <returns></returns>
		/// <remarks></remarks>
		public string ComputeHash(string stringToHash)
		{
			byte[] data = new UTF8Encoding().GetBytes(stringToHash);
			byte[] hashValue = new SHA1Managed().ComputeHash(data);

			return ToHex(hashValue);
		}

		private string ToHex(byte[] bytes)
		{
			var s = new StringBuilder();
			foreach (var b in bytes)
			{
				s.Append(string.Format("{0:x2}", b));
			}
			return s.ToString();
		}

	}
}

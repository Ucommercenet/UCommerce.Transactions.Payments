namespace UCommerce.Transactions.Payments.Payer
{
	/// <summary>
	/// Computes md5 hashes when MD5-key control is enabled.
	/// </summary>
	public class PayerMd5Computer : AbstractMd5Computer
	{
		/// <summary>
		/// Computes a Md5 hash of the 3 parameters
		/// </summary>
		/// <param name="input">The xml data input</param>
		/// <param name="key1">The prefix key.</param>
		/// <param name="key2">The postfix key</param>
		/// <returns>Md5 hash</returns>
		public string GetMd5Key(string input, string key1, string key2)
		{
			return GetMd5Hash(key1 + input + key2);
		}
	}
}
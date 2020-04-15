
namespace Ucommerce.Transactions.Payments.Dibs
{
    /// <summary>
    /// Computes md5 hashes when MD5-key control is enabled.
    /// </summary>
    public class DibsMd5Computer : AbstractMd5Computer
    {
    	/// <summary>
        /// Gets the refund key <paramref name="orderId"/>, <paramref name="transact"/> and <paramref name="amount"/>.
        /// </summary>
        /// <returns>Md5 key.</returns>
        public virtual string GetRefundKey(string orderId, string transact, string amount, string key1, string key2, string merchant)
        {
            return GetMd5Hash(key2 + GetMd5Hash(key1 + string.Format("merchant={0}&orderid={1}&transact={2}&amount={3}", merchant, orderId, transact, amount)));
        }

        /// <summary>
        /// Gets the cancel MD5 key computed by <paramref name="orderId"/> and <paramref name="transact"/>.
        /// </summary>
        /// <returns>md5 key.</returns>
		public virtual string GetCancelMd5Key(string orderId, string transact, string key1, string key2, string merchant)
        {
            return GetMd5Hash(key2 + GetMd5Hash(key1 + string.Format("merchant={0}&orderid={1}&transact={2}", merchant, orderId, transact)));
        }

        /// <summary>
        /// Gets the capture MD5 key computed by <paramref name="orderId"/>, <paramref name="transact"/> and <paramref name="amount"/>.
        /// </summary>
        /// <returns>Md5 key.</returns>
		public virtual string GetCaptureMd5Key(string orderId, string transact, string amount, string key1, string key2, string merchant)
        {
            return GetMd5Hash(key2 + GetMd5Hash(key1 + string.Format("merchant={0}&orderid={1}&transact={2}&amount={3}", merchant, orderId, transact, amount)));
        }

        /// <summary>
        /// Computes a Md5 hash of <paramref name="orderId"/>, <paramref name="currency"/> and <paramref name="amount"/> parameters and Merchant, Key1, Key2 from the configuration.
        /// </summary>
        /// <returns>Md5 hash</returns>
		public virtual string GetPreMd5Key(string orderId, string currency, string amount, string key1, string key2, string merchant)
        {
            const string format = "merchant={0}&orderid={1}&currency={2}&amount={3}";
            var value = string.Format(format, merchant, orderId, currency, amount);
            return GetMd5Hash(key2 + GetMd5Hash(key1 + value));
        }

        /// <summary>
        /// Computes a Md5 hash of <paramref name="transact"/> and <paramref name="amount"/> parameters and Key1, Key2 from the configuration.
        /// </summary>
        /// <returns>Md5 hash</returns>
		public virtual string GetPostMd5Key(string transact, string amount, int currencyNumber, string key1, string key2)
        {
            const string format = "transact={0}&amount={1}&currency={2}";
            var value = string.Format(format, transact, amount, currencyNumber);
            return GetMd5Hash(key2 + GetMd5Hash(key1 + value));
        }
    }
}

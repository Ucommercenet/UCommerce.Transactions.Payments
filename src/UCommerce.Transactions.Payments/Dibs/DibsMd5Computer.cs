using UCommerce.Infrastructure.Configuration;
using UCommerce.Transactions.Payments.Configuration;

namespace UCommerce.Transactions.Payments.Dibs
{
    /// <summary>
    /// Computes md5 hashes when MD5-key control is enabled.
    /// </summary>
    public class DibsMd5Computer : AbstractMd5Computer
    {
        public DibsMd5Computer()
        {
        }

    	/// <summary>
        /// Gets the refund key <paramref name="orderId"/>, <paramref name="transact"/> and <paramref name="amount"/>.
        /// </summary>
        /// <param name="orderId">The order id.</param>
        /// <param name="transact">The transact.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>Md5 key.</returns>
        public virtual string GetRefundKey(string orderId, string transact, string amount, string key1, string key2, string merchant)
        {
            return GetMd5Hash(key2 + GetMd5Hash(key1 + string.Format("merchant={0}&orderid={1}&transact={2}&amount={3}", merchant, orderId, transact, amount)));
        }

        /// <summary>
        /// Gets the cancel MD5 key computed by <paramref name="orderId"/> and <paramref name="transact"/>.
        /// </summary>
        /// <param name="orderId">The order id.</param>
        /// <param name="transact">The transact.</param>
        /// <returns>md5 key.</returns>
		public virtual string GetCancelMd5Key(string orderId, string transact, string key1, string key2, string merchant)
        {
            return GetMd5Hash(key2 + GetMd5Hash(key1 + string.Format("merchant={0}&orderid={1}&transact={2}", merchant, orderId, transact)));
        }

        /// <summary>
        /// Gets the capture MD5 key computed by <paramref name="orderId"/>, <paramref name="transact"/> and <paramref name="amount"/>.
        /// </summary>
        /// <param name="orderId">The order id.</param>
        /// <param name="transact">The transact.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>Md5 key.</returns>
		public virtual string GetCaptureMd5Key(string orderId, string transact, string amount, string key1, string key2, string merchant)
        {
            return GetMd5Hash(key2 + GetMd5Hash(key1 + string.Format("merchant={0}&orderid={1}&transact={2}&amount={3}", merchant, orderId, transact, amount)));
        }

        /// <summary>
        /// Computes a Md5 hash of <paramref name=""/>, <paramref name=""/> and <paramref name="amount"/> 3 parameters and Merchant, Key1, Key2 from <see cref="DibsPaymentMethodServiceConfigurationSection"></see>
        /// </summary>
        /// <param name="orderId">Current order id</param>
        /// <param name="currency">Used currency</param>
        /// <param name="amount">Amount <example>100 for 1 USD or 150 for 1.50 USD</example></param>
        /// <returns>Md5 hash</returns>
		public virtual string GetPreMd5Key(string orderId, string currency, string amount, string key1, string key2, string merchant)
        {
            const string format = "merchant={0}&orderid={1}&currency={2}&amount={3}";
            var value = string.Format(format, merchant, orderId, currency, amount);
            return GetMd5Hash(key2 + GetMd5Hash(key1 + value));
        }

        /// <summary>
        /// </summary>
        /// <param name="transact">Transaction id</param>
        /// <param name="amount">Amount <example>100 for 1 USD or 150 for 1.50 USD</example></param>
        /// <param name="currencyNumber">Currency number</param>
        /// <returns>Md5 hash</returns>
		public virtual string GetPostMd5Key(string transact, string amount, int currencyNumber, string key1, string key2)
        {
            const string format = "transact={0}&amount={1}&currency={2}";
            var value = string.Format(format, transact, amount, currencyNumber);
            return GetMd5Hash(key2 + GetMd5Hash(key1 + value));
        }
    }
}

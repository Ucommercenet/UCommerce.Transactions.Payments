using System.Collections.Generic;
using System.Linq;

namespace Ucommerce.Transactions.Payments.EPay
{
    /// <summary>
    /// Computes md5 hashes when MD5-key control is enabled.
    /// </summary>
    public class EPayMd5Computer : AbstractMd5Computer
    {
	    /// <summary>
	    /// Computes a Md5 hash of the 3 parameters, the MerchantId and a key
	    /// </summary>
	    /// <param name="orderId">Current order id</param>
	    /// <param name="merchantId"></param>
	    /// <param name="currency">Used currency</param>
	    /// <param name="amount">Amount <example>100 for 1 USD or 150 for 1.50 USD</example></param>
		/// <param name="key">The key.</param>
		/// <returns>Md5 hash</returns>
	    public string GetPreMd5Key(string merchantId, string currency, string amount, string orderId, string key)
        {
			return GetMd5Hash(merchantId + orderId + amount + currency + key);
        }

	    public string GetPreMd5Key(string valueToHash)
	    {
		    return GetMd5Hash(valueToHash);
	    }

	    public string GetPreMd5Key(IDictionary<string, string> parameters, string key)
	    {
			return GetMd5Hash(string.Join("", parameters.Select(x => x.Value)) + key);
	    }

	    /// <summary>
	    /// Computes a Md5 hash of the 3 parameters and a key.
	    /// </summary>
	    /// <param name="amount">Amount <example>100 for 1 USD or 150 for 1.50 USD</example></param>
	    /// <param name="orderId">Order id</param>
	    /// <param name="transactionId">Transaction id</param>
	    /// <param name="key">The key.</param>
	    /// <returns>Md5 hash</returns>
	    public string GetPostMd5Key(string amount, string orderId, string transactionId, string key)
        {
			return GetMd5Hash(amount + orderId + transactionId + key);
        }
    }
}

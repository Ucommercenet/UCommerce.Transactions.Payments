using System.Text;

namespace Ucommerce.Transactions.Payments.Quickpay
{
    /// <summary>
    /// Computes md5 hashes for Quickpay md5 checks
    /// </summary>
    public class QuickpayMd5Computer : AbstractMd5Computer
    {
        public QuickpayMd5Computer()
        {
        }

        /// <summary>
        /// Computes md5 hash for pre-authorization check
        /// </summary>
        public string GetPreMd5Key(string protocol, string language, string ordernumber, string amount, string currency, 
            string continueurl, string cancelurl, string callbackurl, string autocapture, string md5Secret,string merchant, string testMode)
        {
	        var sb = new StringBuilder();
	        sb.Append(protocol);
	        sb.Append("authorize");
	        sb.Append(merchant);
	        sb.Append(language);
	        sb.Append(ordernumber);
	        sb.Append(amount);
	        sb.Append(currency);
	        sb.Append(continueurl);
	        sb.Append(cancelurl);
	        sb.Append(callbackurl);
			sb.Append(autocapture);
			sb.Append(testMode);
			sb.Append(md5Secret);

            return GetMd5Hash(sb.ToString());
        }

        /// <summary>
        /// Computes md5 hash from concatenated response fields
        /// </summary>
        public string GetMd5KeyFromResponseValueString(string responseValues,string md5Secret)
        {
			return GetMd5Hash(responseValues + md5Secret);
        }

        /// <summary>
        /// Computes md5 hash for pre-cancel check
        /// </summary>
        public string GetCancelPreMd5Key(string protocol, string transactionId,
			string merchant, string apiKey, string md5Secret)
        {
            var sb = new StringBuilder();
            sb.Append(protocol);
            sb.Append("cancel");
            sb.Append(merchant);
            sb.Append(transactionId);
            sb.Append(apiKey);
			sb.Append(md5Secret);
            
            return GetMd5Hash(sb.ToString());
        }

        /// <summary>
        /// Computes md5 hash for pre-acquire check
        /// </summary>
        public string GetAcquirePreMd5Key(string protocol, string amount, string transactionId,
			string merchant, string apiKey, string md5Secret)
        {
            var sb = new StringBuilder();
            sb.Append(protocol);
            sb.Append("capture");
            sb.Append(merchant);
            sb.Append(amount);
            sb.Append(transactionId);
            sb.Append(apiKey);
            sb.Append(md5Secret);

            return GetMd5Hash(sb.ToString());
        }

        /// <summary>
        /// Computes md5 hash for pre-refund check
        /// </summary>
        public string GetRefundPreMd5Key(string protocol, string amount, string transactionId,
			string merchant, string apiKey, string md5Secret)
        {
            var sb = new StringBuilder();
            sb.Append(protocol);
            sb.Append("refund");
			sb.Append(merchant);
            sb.Append(amount);
            sb.Append(transactionId);
			sb.Append(apiKey);
			sb.Append(md5Secret);

            return GetMd5Hash(sb.ToString());
        }
    }
}
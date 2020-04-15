using System.Collections.Generic;
using Ucommerce.Transactions.Payments.Configuration;
using System.Linq;

namespace Ucommerce.Transactions.Payments.SagePay
{
    /// <summary>
    /// Computes md5 hashes when MD5-key control is enabled.
    /// </summary>
    public class SagePayMd5Computer : AbstractMd5Computer
    {
        public virtual string VpsSignature(IList<string> list)
        {
            var concat = string.Concat(list.ToArray());
            return GetMd5Hash(concat).ToUpper();
        }
    }
}
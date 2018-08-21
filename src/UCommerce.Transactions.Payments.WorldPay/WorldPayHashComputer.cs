using System.Collections.Generic;
using System.Linq;
using UCommerce.Transactions.Payments.Common;

namespace UCommerce.Transactions.Payments.WorldPay
{
	public class WorldPayMd5Computer : AbstractMd5Computer
	{
        public string GetSignatureHash(IList<string> list, string signature)
        {
            list.Insert(0, signature);
            return GetMd5Hash(string.Join(":", list.ToArray()));
        }

		public string GetHash(decimal amount, string referenceId, string currency, string key)
		{
			return GetMd5Hash(key + amount.ToCents() + referenceId + currency);
		}
	}
}
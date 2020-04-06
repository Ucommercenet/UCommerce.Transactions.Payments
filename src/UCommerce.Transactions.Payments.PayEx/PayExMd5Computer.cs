using Ucommerce.Infrastructure.Configuration;
using Ucommerce.Transactions.Payments.Configuration;

namespace Ucommerce.Transactions.Payments.PayEx
{
	public class PayExMd5Computer : AbstractMd5Computer
	{
		public PayExMd5Computer() { }

		public string GetPreHash(string input, string sectionKey)
		{
			string md5Hash = GetMd5Hash(input + sectionKey);

			return md5Hash;
		}
	}
}
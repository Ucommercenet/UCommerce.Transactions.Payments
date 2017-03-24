using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.SecureTrading
{
	public class SecureTradingMd5Computer : AbstractMd5Computer
	{
		public SecureTradingMd5Computer()
		{
			
		}


		public virtual string GetComputedMd5Hash(string input)
		{
			return this.GetMd5Hash(input);
		}
	}
}

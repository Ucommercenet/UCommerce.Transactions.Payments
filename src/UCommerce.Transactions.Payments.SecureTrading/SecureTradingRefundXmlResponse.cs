using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Ucommerce.Transactions.Payments.SecureTrading
{
	public class SecureTradingRefundXmlResponse : SecureTradingResponse
	{
		public SecureTradingRefundXmlResponse(string xml) : base(xml)
		{
			
		}
	}
}

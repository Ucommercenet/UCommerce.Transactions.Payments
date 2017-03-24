using System;
using System.Xml;

namespace UCommerce.Transactions.Payments.SecureTrading
{

	public class SecureTradingTransactionQueryXmlResponse : SecureTradingResponse
	{
		public SecureTradingTransactionQueryXmlResponse(string xml) : base(xml)
		{

		}

		public SecureTradingSettlementStatus SettleStatus
		{
			get
			{
				return TryParsSecureTradingSettlementStatus("/responseblock/response/record/settlement/settlestatus");
			}
		}

		public string CurrencyCode
		{
			get { return GetPropertyFromXml("/responseblock/response/record/billing/amount", "currencycode"); }
		}

		public string TransactionId
		{
			get { return GetStringFromXml("/responseblock/response/record/transactionreference"); }
		}

		public string OrderReference
		{
			get { return GetStringFromXml("/responseblock/response/record/merchant/orderreference"); }
		}

		public bool TransactionFound
		{
			get { return Convert.ToInt32(GetStringFromXml("/responseblock/response/found")) > 0; }
		}
	}
}

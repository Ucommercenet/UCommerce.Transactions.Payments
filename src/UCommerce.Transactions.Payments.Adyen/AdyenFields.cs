using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.Adyen
{
	internal static class AdyenFields
	{
		public const string PaymentAmount = "paymentAmount";
		public const string CurrenctCode = "currencyCode";
		public const string ShipBeforeData = "shipBeforeDate";
		public const string MerchantReference = "merchantReference";
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class PaymentProductData : IApiDataPartReadOnly, IPaymentProduct
	{
		public string PaymentMethodName { get; private set; }

		public int PaymentProductId { get; private set; }

		public string PaymentProductName { get; private set; }

		public long? MinimumAmount { get; private set; }

		public long? MaximumAmount { get; private set; }

		public string CurrencyCode { get; private set; }

		public int OrderTypeIndicator { get; private set; }

		public string PaymentProductLogo { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			PaymentMethodName = doc.GetStringFromXml(path + "/ROW/PAYMENTMETHODNAME");
			PaymentProductId = doc.GetIntFromXml(path + "/ROW/PAYMENTPRODUCTID");
			PaymentProductName = doc.GetStringFromXml(path + "/ROW/PAYMENTPRODUCTNAME");
			MinimumAmount = doc.GetNullableLongFromXml(path + "/ROW/MINAMOUNT");
			MaximumAmount = doc.GetNullableLongFromXml(path + "/ROW/MAXAMOUNT");
			CurrencyCode = doc.GetStringFromXml(path + "/ROW/CURRENCYCODE");
			OrderTypeIndicator = doc.GetIntFromXml(path + "/ROW/ORDERTYPEINDICATOR");
			PaymentProductLogo = doc.TryGetStringFromXml(path + "/ROW/PAYMENTPRODUCTLOGO");
		}
	}
}

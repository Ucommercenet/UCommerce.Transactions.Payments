using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class PaymentData : IApiDataPartReadOnly, IPaymentData
	{
		public long StatusDate { get; private set; }

		public string PaymentReference { get; private set; }

		public string AdditionalReference { get; private set; }

		public long OrderId { get; private set; }

		public string ExternalReference { get; private set; }

		public int EffortId { get; private set; }

		public string Ref { get; private set; }

		public string FormAction { get; private set; }

		public string FormMethod { get; private set; }

		public int AttemptId { get; private set; }

		public int MerchantId { get; private set; }

		public int StatusId { get; private set; }

		public string ReturnMac { get; private set; }

		public string Mac { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			StatusDate = doc.GetLongFromXml(path + "/ROW/STATUSID");
			PaymentReference = doc.GetStringFromXml(path + "/ROW/PAYMENTREFERENCE");
			AdditionalReference = doc.GetStringFromXml(path + "/ROW/ADDITIONALREFERENCE");
			OrderId = doc.GetLongFromXml(path + "/ROW/ORDERID");
			ExternalReference = doc.GetStringFromXml(path + "/ROW/EXTERNALREFERENCE");
			EffortId = doc.GetIntFromXml(path + "/ROW/EFFORTID");
			Ref = doc.GetStringFromXml(path + "/ROW/REF");
			FormAction = doc.GetStringFromXml(path + "/ROW/FORMACTION");
			FormMethod = doc.GetStringFromXml(path + "/ROW/FORMMETHOD");
			AttemptId = doc.GetIntFromXml(path + "/ROW/ATTEMPTID");
			MerchantId = doc.GetIntFromXml(path + "/ROW/MERCHANTID");
			StatusId = doc.GetIntFromXml(path + "/ROW/STATUSID");
			ReturnMac = doc.GetStringFromXml(path + "/ROW/RETURNMAC");
			Mac = doc.GetStringFromXml(path + "/ROW/MAC");
		}
	}
}

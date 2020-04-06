using System;
using System.Xml;

namespace Ucommerce.Transactions.Payments.PayEx
{
	public class PayExXmlMessage
	{
		public PayExXmlMessage(string xml)
		{
			_doc = new XmlDocument();
			_doc.LoadXml(xml);
		}

		private readonly XmlDocument _doc;

		private string GetStringFromXml(string nodePath)
		{
			var myNode = _doc.SelectSingleNode(nodePath);

			if (myNode == null)
				throw new NullReferenceException("XmlNode is null");

			return myNode.InnerText;
		}

		public bool StatusCode
		{
			get
			{
				var stringFromXml = GetStringFromXml("/payex/status/errorCode");
				return stringFromXml.Equals("OK", StringComparison.OrdinalIgnoreCase);
			}
		}

		public string ErrorDescription
		{
			get { return GetStringFromXml("/payex/status/description"); }
		}

		public bool AlreadyCompleted
		{
			get
			{
				var stringFromXml = GetStringFromXml("/payex/alreadyCompleted");
				return stringFromXml.Equals("true", StringComparison.OrdinalIgnoreCase);
			}
		}

		public string RedirectUrl
		{
			get { return GetStringFromXml("/payex/redirectUrl"); }
		}

		public int TransactionNumber
		{
			get
			{
				var stringFromXml = GetStringFromXml("/payex/transactionNumber");
				int transactionNumber;
				if (int.TryParse(stringFromXml, out transactionNumber))
					return transactionNumber;

				throw new Exception(string.Format("Could not parse: {0} as an integer", stringFromXml));
			}
		}

		public int OriginalTransactionNumber
		{
			get
			{
				var stringFromXml = GetStringFromXml("/payex/originalTransactionNumber");
				int transactionNumber;
				if (int.TryParse(stringFromXml, out transactionNumber))
					return transactionNumber;

				throw new Exception(string.Format("Could not parse: {0} as an integer", stringFromXml));
			}
		}

		public int TransactionStatus
		{
			get
			{
				var stringFromXml = GetStringFromXml("/payex/transactionStatus");
				int transactionNumber;
				if (int.TryParse(stringFromXml, out transactionNumber))
					return transactionNumber;

				throw new Exception(string.Format("Could not parse: {0} as an integer", stringFromXml));
			}
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Ucommerce.Transactions.Payments.SecureTrading
{
	public abstract class SecureTradingResponse
	{
		private readonly XmlDocument _doc;
		private readonly string _xmlResponse;

		protected SecureTradingResponse(string xml)
		{
			_doc = new XmlDocument();
			_doc.LoadXml(xml);
			_xmlResponse = xml;
		}

		protected virtual XmlNode GetNodeFromDoc(string nodePath)
		{
			var xmlNode = _doc.SelectSingleNode(nodePath);

			if (xmlNode == null)
			{
				throw new NullReferenceException(string.Format("XmlNode is null. Looked for node in path: {0} in xml response:\r\n {1}",nodePath,_xmlResponse));				
			}

			return xmlNode;
		}

		protected virtual string GetStringFromXml(string nodePath)
		{
			return GetNodeFromDoc(nodePath).InnerText;
		}

		protected virtual string GetPropertyFromXml(string nodePath, string propertyName)
		{
			var node = GetNodeFromDoc(nodePath);

			if (node.Attributes[propertyName] == null) throw new InvalidOperationException("Response didn't contain currency");

			return GetNodeFromDoc(nodePath).Attributes[propertyName].Value;
		}

		protected virtual SecureTradingErrorCode TryParseSecureTradingErrorCodeFromValue(string nodePath)
		{
			var value = GetStringFromXml(nodePath);

			SecureTradingErrorCode status;

			var success = SecureTradingErrorCode.TryParse(value, out status);
			if (success)
				return status;

			throw new InvalidOperationException(string.Format("Could not convert response to know error code. Response was: {0}", value));
		}

		protected virtual SecureTradingSettlementStatus TryParsSecureTradingSettlementStatus(string nodePath)
		{
			var value = GetStringFromXml(nodePath);

			SecureTradingSettlementStatus status;

			var success = SecureTradingSettlementStatus.TryParse(value, out status);
			if (success)
				return status;

			throw new InvalidOperationException(string.Format("Could not convert response to know status. Response was: {0}", value));
		}

		public SecureTradingErrorCode ErrorCode
		{
			get { return TryParseSecureTradingErrorCodeFromValue("responseblock/response/error/code"); }
		}

		public string ErrorMessage
		{
			get { return GetStringFromXml("/responseblock/response/error/message"); }
		}

		public bool Success
		{
			get { return ErrorCode == SecureTradingErrorCode.Success; }
		}

		public bool Declined
		{
			get { return ErrorCode == SecureTradingErrorCode.Declined; }
		}

		public string XmlResponse
		{
			get { return _xmlResponse; }
		}
	}
}

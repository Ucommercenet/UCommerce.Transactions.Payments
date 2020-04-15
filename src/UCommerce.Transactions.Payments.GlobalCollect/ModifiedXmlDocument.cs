using System;
using System.Collections.Generic;
using System.Xml;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect
{
	public class ModifiedXmlDocument
	{
		private readonly XmlDocument _doc;
		private readonly string _xmlResponse;

		public ModifiedXmlDocument(string xml)
		{
			_doc = new XmlDocument();
			_doc.LoadXml(xml);
			_xmlResponse = xml;
		}

		public virtual XmlNode GetNodeFromDoc(string nodePath)
		{
			var xmlNode = _doc.SelectSingleNode(nodePath);

			if (xmlNode == null)
			{
				throw new NullReferenceException(string.Format("XmlNode is null. Looked for node in path: {0} in xml response:\r\n {1}.",nodePath,_xmlResponse));				
			}

			return xmlNode;
		}

		public virtual bool Exists(string path)
		{
			return _doc.SelectSingleNode(path) != null;
		}

		public virtual string GetStringFromXml(string nodePath)
		{
			return GetNodeFromDoc(nodePath).InnerText;
		}

		public virtual string TryGetStringFromXml(string nodePath)
		{
			var xmlNode = _doc.SelectSingleNode(nodePath);

			if (xmlNode == null)
			{
				return null;
			}

			return xmlNode.InnerText;
		}

		public virtual int GetIntFromXml(string nodePath)
		{
			var intAsString = GetStringFromXml(nodePath);
			return int.Parse(intAsString);
		}

		public virtual long GetLongFromXml(string nodePath)
		{
			var longAsString = GetStringFromXml(nodePath);
			return long.Parse(longAsString);
		}

		public virtual int? GetNullableIntFromXml(string nodePath)
		{
			var xmlNode = _doc.SelectSingleNode(nodePath);
			if (xmlNode == null)
			{
				return null;
			}

			int result;
			if (int.TryParse(xmlNode.InnerText, out result))
			{
				return result;
			}

			return null;
		}

		public virtual long? GetNullableLongFromXml(string nodePath)
		{
			var xmlNode = _doc.SelectSingleNode(nodePath);
			if (xmlNode == null)
			{
				return null;
			}

			long result;
			if (long.TryParse(xmlNode.InnerText, out result))
			{
				return result;
			}

			return null;
		}

		public virtual IEnumerable<ModifiedXmlDocument> GetNodes(string nodePath)
		{
			var result = new List<ModifiedXmlDocument>();

			var nodes = _doc.SelectNodes(nodePath);
			if (nodes == null) { return result; } 

			foreach (XmlNode node in nodes)
			{
				var doc = new ModifiedXmlDocument(node.OuterXml);
				result.Add(doc);
			}

			return result;
		}
	}
}

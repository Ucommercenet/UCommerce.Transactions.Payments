using System.Collections.Generic;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
{
	public class ErrorChecker : IApiDataPartReadOnly
	{
		public ErrorChecker()
		{
			Meta = new ResponseMeta();
			Errors = new List<ErrorRow>();
		}

		public ErrorChecker(string text) : this()
		{
			FromModifiedXml(new ModifiedXmlDocument(text), string.Empty);
		}

		public string Result { get; private set; }

		public ResponseMeta Meta { get; private set; }

		public IList<ErrorRow> Errors { get; private set; }
		
		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Result = doc.GetStringFromXml(path + "XML/REQUEST/RESPONSE/RESULT");
			Meta.FromModifiedXml(doc, path + "XML/REQUEST/RESPONSE");

			Errors.Clear();
			foreach (var node in doc.GetNodes(path + "XML/REQUEST/RESPONSE/ERROR"))
			{
				var row = new ErrorRow();
				row.FromModifiedXml(node, string.Empty);
				Errors.Add(row);
			}
		}
	}
}

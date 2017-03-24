using System.Collections.Generic;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class Params : IApiDataPart
	{
		private readonly IList<IApiDataPart> _parameters;

		public Params()
		{
			_parameters = new List<IApiDataPart>();
		}

		public IList<IApiDataPart> Parameters
		{
			get { return _parameters; }
		}

		public void AddToStringBuilder(StringBuilder sb)
		{
			sb.Append("<PARAMS>");
			foreach (var parameter in _parameters)
			{
				parameter.AddToStringBuilder(sb);
			}
			sb.Append("</PARAMS>");
		}

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			foreach (var parameter in _parameters)
			{
				parameter.FromModifiedXml(doc, path + "/PARAMS");
			}
		}
	}
}

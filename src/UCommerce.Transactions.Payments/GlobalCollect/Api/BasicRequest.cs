using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
{
	public class BasicRequest : IApiDataPart
	{
		public BasicRequest(string action)
		{
			Action = new ApiAction(action);
			Meta = new Meta();
			Params = new Params();
		}

		public ApiAction Action { get; private set; }

		public Meta Meta { get; private set; }

		protected Params Params { get; set; }

		public virtual void AddToStringBuilder(StringBuilder sb)
		{
			sb.Append("<XML><REQUEST>");
			Action.AddToStringBuilder(sb);
			Meta.AddToStringBuilder(sb);
			Params.AddToStringBuilder(sb);
			sb.Append("</REQUEST></XML>");
		}

		public virtual void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Action.FromModifiedXml(doc, "XML/REQUEST");
			Meta.FromModifiedXml(doc, "XML/REQUEST");
			Params.FromModifiedXml(doc, "XML/REQUEST");
		}
		public override string ToString()
		{
			var sb = new StringBuilder();
			AddToStringBuilder(sb);
			return sb.ToString();
		}
	}
}

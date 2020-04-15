using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class ResponseMeta : IApiDataPartReadOnly
	{
		public long RequestId { get; private set; }

		public long ResponseDatetime { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			RequestId = doc.GetLongFromXml(path + "/META/REQUESTID");
			ResponseDatetime = doc.GetLongFromXml(path + "/META/RESPONSEDATETIME");
		}
	}
}

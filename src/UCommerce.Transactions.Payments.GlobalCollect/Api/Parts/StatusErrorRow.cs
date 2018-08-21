using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class StatusErrorRow : IApiDataPartReadOnly
	{
		public string Type { get; private set; }

		public int Code { get; private set; }

		public string Message { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Type = doc.GetStringFromXml("/ERROR/TYPE");
			Code = doc.GetIntFromXml(path + "/ERROR/CODE");
			Message = doc.GetStringFromXml(path + "/ERROR/MESSAGE");
		}
	}
}

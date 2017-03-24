using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Xslt.Resources;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class ErrorRow : IApiDataPartReadOnly
	{
		public int Code { get; private set; }

		public string Message { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			Code = doc.GetIntFromXml(path + "/ERROR/CODE");
			Message = doc.GetStringFromXml(path + "/ERROR/MESSAGE");
		}
	}
}

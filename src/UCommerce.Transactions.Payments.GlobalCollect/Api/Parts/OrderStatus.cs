using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class OrderStatus : IApiDataPartReadOnly, IOrderStatus
	{
		public OrderStatus()
		{
			Errors = new List<StatusErrorRow>();
		}

		public OrderStatus(ModifiedXmlDocument doc, string path) : this()
		{
			FromModifiedXml(doc, path);
		}

		public string StatusDate { get; set; }

		public int PaymentMethodId { get; set; }

		public string MerchantReference { get; set; }

		public int AttemptId { get; set; }

		public string PaymentReference { get; set; }

		public long Amount { get; set; }

		public int MerchantId { get; set; }

		public long OrderId { get; set; }

		public int StatusId { get; set; }

		public int EffortId { get; set; }

		public string CurrencyCode { get; set; }

		public int PaymentProductId { get; set; }

		public IList<StatusErrorRow> Errors { get; private set; }

		public void FromModifiedXml(ModifiedXmlDocument doc, string path)
		{
			StatusDate = doc.GetStringFromXml(path + "/STATUSDATE");
			PaymentMethodId = doc.GetIntFromXml(path + "/PAYMENTMETHODID");
			OrderId = doc.GetLongFromXml(path + "/ORDERID");
			EffortId = doc.GetIntFromXml(path + "/EFFORTID");
			AttemptId = doc.GetIntFromXml(path + "/ATTEMPTID");
			MerchantId = doc.GetIntFromXml(path + "/MERCHANTID");
			StatusId = doc.GetIntFromXml(path + "/STATUSID");

			Errors.Clear();
			foreach (var node in doc.GetNodes(path + "/ERRORS/ERROR"))
			{
				var row = new StatusErrorRow();
				row.FromModifiedXml(node, string.Empty);
				Errors.Add(row);
			}
		}
	}
}

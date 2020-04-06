using System;
using System.Collections.Generic;
using System.Web;

namespace Ucommerce.Transactions.Payments.Adyen
{
	public enum PaymentEvent
	{
		  // Normal payment events
		Authorization,
		  // Modification payment events
		Cancellation,
		Refund,
		CancelOrRefund,
		Capture,
		RefundedReversed,
		CaptureFailed,
		RefundFailed,
		  // Dispute events
		RequestForInformation,
		NotificationOfChargeback,
		AdviceOfDebit,
		Chargeback,
		ChargebackReversed,
		  // Other events
		ReportAvailable
	}

	public enum ModificationOperation
	{
		Capture,
		Refund,
		Cancel
	}

	public class NotificationMessageData
	{
		public bool Live { get; set; }
		public PaymentEvent EventCode { get; set; }
		public string PspReference { get; set; }
		public string OriginalReference { get; set; }
		public string MerchantReference { get; set; }
		public string MerchantAccountCode { get; set; }
		public DateTime EventDate { get; set; }
		public bool Success { get; set; }
		public string PaymentMethod { get; set; }
		public ModificationOperation[] Operations { get; set; }
		public string Reason { get; set; }
		public string CurrencyCode { get; set; }
		public decimal Amount { get; set; }

		public void ExtractDataFromRequest(IDictionary<string, string> dict)
		{
			Live = BuildBoolean(GetValue(dict, "live"));
			EventCode = BuildEventCode(GetValue(dict, "eventCode"));
			PspReference = GetValue(dict, "pspReference");
			OriginalReference = GetValue(dict, "originalReference");
			MerchantReference = GetValue(dict, "merchantReference");
			MerchantAccountCode = GetValue(dict, "merchantAccountCode");
			EventDate = BuildEventDate(GetValue(dict, "eventDate"));
			Success = BuildBoolean(GetValue(dict, "success"));
			PaymentMethod = GetValue(dict, "paymentMethod");
			Operations = BuildOperations(GetValue(dict, "operations"));
			Reason = GetValue(dict, "reason");
			CurrencyCode = GetValue(dict, "currency");
			Amount = BuildAmount(GetValue(dict, "value"));
		}

		private bool BuildBoolean(string s)
		{
			return s.Equals("true", StringComparison.InvariantCultureIgnoreCase);
		}

		private decimal BuildAmount(string s)
		{
			decimal d;
			decimal.TryParse(s, out d);
			d /= 100; // Convert back from "cents".
			return d;
		}

		private ModificationOperation[] BuildOperations(string s)
		{
			string[] splits = s.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries);
			
			var operations = new ModificationOperation[splits.Length];
			
			for (int i = 0; i < splits.Length; i++)
			{
				operations[i] = BuildOperation(splits[i]);
			}

			return operations;
		}

		private ModificationOperation BuildOperation(string s)
		{
			s = s.Trim();
			switch (s)
			{
				case "CAPTURE": return ModificationOperation.Capture;
				case "REFUND": return ModificationOperation.Refund;
				case "CANCEL": return ModificationOperation.Cancel;
				default: throw new NotSupportedException(string.Format("'{0}' is not a supported Operation.", s));
			}
		}

		private DateTime BuildEventDate(string s)
		{
			DateTime dt;
			DateTime.TryParse(s, out dt);
			return dt;
		}

		private PaymentEvent BuildEventCode(string s)
		{
			s = s.Trim();
			switch (s)
			{
				case "AUTHORISATION": return PaymentEvent.Authorization;
				case "CANCELLATION": return PaymentEvent.Cancellation;
				case "REFUND": return PaymentEvent.Refund;
				case "CANCEL_OR_REFUND": return PaymentEvent.CancelOrRefund;
				case "CAPTURE": return PaymentEvent.Capture;
				case "REFUNDED_REVERSED": return PaymentEvent.RefundedReversed;
				case "CAPTURE_FAILED": return PaymentEvent.CaptureFailed;
				case "REFUND_FAILED": return PaymentEvent.RefundFailed;
				case "REQUEST_FOR_INFORMATION": return PaymentEvent.RequestForInformation;
				case "NOTIFICATION_OF_CHARGEBACK": return PaymentEvent.NotificationOfChargeback;
				case "ADVICE_OF_DEBIT": return PaymentEvent.AdviceOfDebit;
				case "CHARGEBACK": return PaymentEvent.Chargeback;
				case "CHARGEBACK_REVERSED": return PaymentEvent.ChargebackReversed;
				case "REPORT_AVAILABLE": return PaymentEvent.ReportAvailable;
				default: throw new NotSupportedException(string.Format("'{0}' is not a supported Payment Event Code.", s));
			}
		}

		private string GetValue(IDictionary<string, string> dict, string key)
		{
			if (dict.ContainsKey(key)) return dict[key];
			return string.Empty;
		}
	}
}

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Web;
using com.paypal.sdk.profiles;
using com.paypal.sdk.services;
using com.paypal.sdk.util;
using Ucommerce.Extensions;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Transactions.Payments.Common;

namespace Ucommerce.Transactions.Payments.PayPal
{
	public class PayPalPaymentMethodService : ExternalPaymentMethodService
	{
		protected ILoggingService LoggingService { get; set; }
		protected PayPalWebSitePaymentsStandardPageBuilder PageBuilder { get; set; }

		public PayPalPaymentMethodService(
			PayPalWebSitePaymentsStandardPageBuilder pageBuilder, ILoggingService loggingService)
		{
			PageBuilder = pageBuilder;
			LoggingService = loggingService;
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		protected bool IsValidCallback(PaymentMethod paymentMethod)
		{
			// Create the request back
			var request = (HttpWebRequest)WebRequest.Create(PageBuilder.GetPostUrl(paymentMethod));

			// Set values for the request back
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";

			string strFormValues = Encoding.ASCII.GetString(HttpContext.Current.Request.BinaryRead(HttpContext.Current.Request.ContentLength));

			string validateParameter = strFormValues + "&cmd=_notify-validate";

			request.ContentLength = validateParameter.Length;

			// String.Compare()
			// Write the request back IPN strings
			var writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII);
			writer.Write(RuntimeHelpers.GetObjectValue(validateParameter));
			writer.Close();

			//send the request, read the response
			var response = (HttpWebResponse)request.GetResponse();

			Stream responseStream = response.GetResponseStream();
			var reader = new StreamReader(responseStream, Encoding.UTF8);

			string readToEnd = reader.ReadToEnd();

			if(readToEnd == "VERIFIED")
				return true;

			// INVALID - false value from Paypal

			return false;
		}

		public override void ProcessCallback(Payment payment)
		{
			var request = HttpContext.Current.Request;

			if (!IsValidCallback(payment.PaymentMethod))
			{
				string message = string.Format("Could not validate IPN from PayPal. TransactionId {0}. Request received {1} at {2}.", request["txn_id"], request.Form, request.RawUrl);
				LoggingService.Debug<PayPalPaymentMethodService>(message);
				throw new SecurityException(message);
			}

			string transactParameter = request["txn_id"];
			if (string.IsNullOrEmpty(transactParameter))
				throw new ArgumentException(@"txn_id must be present in query string.");

			payment.TransactionId = transactParameter;

			//bool authCallBack = string.Equals(request.Form["transaction_entity"], "auth", StringComparison.OrdinalIgnoreCase);

			// "payment_status" may have the following values:
			//	Canceled_Reversal
			//	Completed (Success)
			//	Denied (previous will be pending)
			//	Expired
			//	Failed
			//	Pending - will get a "completed" or "failed" IPN later
			//	Processed (means API request was processed, will get a Completed afterwards)
			//	Refunded
			//	Reversed
			//	Voided
			bool remotePaymentIsCompleted = RemotePaymentStatusIsOkToCompleteOrder(request);

			bool localPaymentIsPendingAuth = payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.PendingAuthorization;
			if (remotePaymentIsCompleted && localPaymentIsPendingAuth)
			{
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}
			
			payment.Save();
		}

		/// <summary>
		/// Verfies that the remote payment status is OK to complete the order.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <returns></returns>
		/// <remarks>
		/// Payment status will either be: A) completed, or B) pending. If it's
		/// pending because we're authorizing it's OK to complete the payment.
		/// 
		/// Any other combinations of remote "pending" payment status and pending reason
		/// than "pendingauthorization" are not considered valid for completing the
		/// payment.
		/// </remarks>
		private bool RemotePaymentStatusIsOkToCompleteOrder(HttpRequest request)
		{
			string remotePaymentStatus = (request.Form["payment_status"] ?? "").ToLower();
			string pendingReason = (request.Form["pending_reason"] ?? "").ToLower();

			return remotePaymentStatus == "completed"
			       || (remotePaymentStatus == "pending" && pendingReason == "authorization");
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			var caller = GetPayPalNVPCaller(payment.PaymentMethod);

			var postCodec = new NVPCodec();
			postCodec["VERSION"] = "51.0";
			postCodec["METHOD"] = "DoVoid";
			postCodec["AUTHORIZATIONID"] = payment.TransactionId;
			postCodec["TRXTYPE"] = "V";

			// Execute the API operation and obtain the response.
			string postString = postCodec.Encode();
			string responseString = caller.Call(postString);

			var responseCodec = new NVPCodec();
			responseCodec.Decode(responseString);
			
			status = GetCodecStatus(responseCodec);
			bool callStatus = GetCallStatus(responseCodec);

			if (callStatus)
				status = PaymentMessages.CancelSuccess + " >> " + status;
			else
				status = PaymentMessages.CancelFailed + " >> " + status;

			return callStatus;
		}

		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			var caller = GetPayPalNVPCaller(payment.PaymentMethod);

			var postCodec = new NVPCodec();
			postCodec["VERSION"] = "51.0";
			postCodec["METHOD"] = "DoCapture";
			postCodec["TRXTYPE"] = "D"; // D = Delay caputure // C = Refund // A = Auth
			postCodec["AUTHORIZATIONID"] = payment.TransactionId;
			postCodec["COMPLETETYPE"] = "Complete";
			postCodec["AMT"] = payment.Amount.ToString("#.00", CultureInfo.InvariantCulture);
			postCodec["CURRENCYCODE"] = payment.PurchaseOrder.BillingCurrency.ISOCode;

			// Execute the API operation and obtain the response.
			string postString = postCodec.Encode();
			string responseString = caller.Call(postString);

			var responseCodec = new NVPCodec();
			responseCodec.Decode(responseString);

			status = GetCodecStatus(responseCodec);
			bool callStatus = GetCallStatus(responseCodec);

			if (callStatus)
			{
				status = PaymentMessages.AcquireSuccess + " >> " + status;
				
				// Update with the new transaction.
				// We'll need it for refund.
				payment.TransactionId = responseCodec["TRANSACTIONID"];
			}
			else
				status = PaymentMessages.AcquireFailed + " >> " + status;

			return callStatus;
		}

		private static bool GetCallStatus(NVPCodec codec)
		{
			string strAck = codec["ACK"];
			return (strAck != null && (strAck.Equals("Success", StringComparison.OrdinalIgnoreCase) || strAck.Equals("SuccessWithWarning", StringComparison.OrdinalIgnoreCase)));
		}

		private static string GetCodecStatus(NVPCodec codec)
		{
			var stringBuilder = new StringBuilder();
			foreach (string s in codec)
			{
				stringBuilder.AppendLine(string.Format("{0} => {1}", s, codec[s]));
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <remarks>
		/// Docs at https://developer.paypal.com/webapps/developer/docs/classic/api/merchant/RefundTransaction_API_Operation_NVP/
		/// </remarks>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			var caller = GetPayPalNVPCaller(payment.PaymentMethod);

			var postCodec = new NVPCodec();
			postCodec["VERSION"] = "51.0";
			postCodec["METHOD"] = "RefundTransaction";
			postCodec["TRANSACTIONID"] = payment.TransactionId;
			postCodec["REFUNDTYPE"] = "Full";

			// Execute the API operation and obtain the response.
			string postString = postCodec.Encode();
			string responseString = caller.Call(postString);

			var responseCodec = new NVPCodec();
			responseCodec.Decode(responseString);
			
			status = GetCodecStatus(responseCodec);
			bool callStatus = GetCallStatus(responseCodec);

			if (callStatus)
				status = PaymentMessages.RefundSuccess + " >> " + status;
			else
				status = PaymentMessages.RefundFailed + " >> " + status;

			return callStatus;

		}

		private NVPCallerServices GetPayPalNVPCaller(PaymentMethod paymentMethod)
		{
			// var callerServices = new com.paypal.sdk.services.CallerServices();
			var caller = new NVPCallerServices();

			IAPIProfile profile = ProfileFactory.createSignatureAPIProfile();
			profile.Environment = paymentMethod.DynamicProperty<bool>().Sandbox ? "sandbox" : "live";
			profile.APIPassword = paymentMethod.DynamicProperty<string>().ApiPassword;
			profile.APISignature = paymentMethod.DynamicProperty<string>().ApiSignature;
			profile.APIUsername = paymentMethod.DynamicProperty<string>().ApiUsername;

			caller.APIProfile = profile;
			return caller;
		}
		
	}
}

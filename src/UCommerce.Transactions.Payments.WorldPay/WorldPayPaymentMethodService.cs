using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.WorldPay
{
	public class WorldPayPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private WorldPayPageBuilder PageBuilder { get; set; }
		private WorldPayMd5Computer Md5Computer { get; set; }

		public WorldPayPaymentMethodService(WorldPayPageBuilder pageBuilder, WorldPayMd5Computer md5Computer, IAbsoluteUrlService absoluteUrlService)
		{
			_absoluteUrlService = absoluteUrlService;
			PageBuilder = pageBuilder;
			Md5Computer = md5Computer;
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		public override void ProcessCallback(Payment payment)
		{
			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
				return;

			var request = HttpContext.Current.Request;

			string callbackPwFromConfiguration = payment.PaymentMethod.DynamicProperty<string>().CallbackPW;
			string key = payment.PaymentMethod.DynamicProperty<string>().Key;
			bool instantCapture = payment.PaymentMethod.DynamicProperty<bool>().InstantCapture;
			string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string declineUrl = payment.PaymentMethod.DynamicProperty<string>().DeclineUrl;

			if (!string.IsNullOrEmpty(callbackPwFromConfiguration))
			{
				var callbackPwFromRequest = request["callbackPW"];
				if (string.IsNullOrEmpty(callbackPwFromRequest))
				{
					throw new NullReferenceException("callbackPW from the HttpRequest is null.");
				}

				if (!callbackPwFromRequest.Equals(callbackPwFromConfiguration, StringComparison.OrdinalIgnoreCase))
				{
					throw new SecurityException(string.Format("Callback password does not match. From configuration: {0}, From server callback: {1}.", callbackPwFromConfiguration, callbackPwFromRequest));
				}
			}

			var hash = request["MC_hash"];
			if (string.IsNullOrEmpty(hash))
				throw new SecurityException("MC_hash is empty.");

			var s = Md5Computer.GetHash(payment.Amount, payment.ReferenceId, payment.PurchaseOrder.BillingCurrency.ISOCode, key);

			if (!hash.Equals(s, StringComparison.OrdinalIgnoreCase))
				throw new SecurityException("Hashes do not match, message tampered with.");

			string transStatus = request["transStatus"];
			if (string.IsNullOrEmpty(transStatus))
				throw new NullReferenceException("transStatus was null or empty.");

			if (transStatus[0] == 'Y') //A value of Y indicates that the transaction has been authorised
			{
				// the authorisation mode is incorrect
				string transactParameter = request["transId"];
				if (string.IsNullOrEmpty(transactParameter))
					throw new ArgumentException(@"transId must be present in query string.");

				payment.TransactionId = transactParameter;

				if (instantCapture)
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
				else
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);

				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

				if (!string.IsNullOrEmpty(acceptUrl))
					HttpContext.Current.Response.Write(DownloadPageContent(GetLocalhostSafeCallbackUrl(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(payment.PurchaseOrder)));
			}
			else if (transStatus[0] == 'C') // a value of C means that the transaction was cancelled
			{
				if (!string.IsNullOrEmpty(declineUrl))
					HttpContext.Current.Response.Write(DownloadPageContent(GetLocalhostSafeCallbackUrl(_absoluteUrlService.GetAbsoluteUrl(declineUrl)).AddOrderGuidParameter(payment.PurchaseOrder)));
			}
			else
			{
				throw new NotSupportedException("transStatus should have a status of either 'Y' or 'C'.");
			}
		}
		private Uri GetLocalhostSafeCallbackUrl(string url)
		{
			var request = HttpContext.Current.Request;
			return new Uri(url.Replace("://localhost/", $"://localhost:{request.Url.Port}/"));
		}

		private string DownloadPageContent(Uri uri)
		{
			var client = new WebClient();
			var requestedHtml = client.DownloadData(uri);
			var encoding = new UTF8Encoding();
			return encoding.GetString(requestedHtml);
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			payment.TransactionId = PaymentMessages.CancelledLocally;
			status = PaymentMessages.CancelNotAutomatic;
			return true;
		}

		private string GetPostUrl(PaymentMethod paymentMethod)
		{
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;

			if (testMode)
				return "https://select-test.worldpay.com/wcc/itransaction";

			return "https://select.worldpay.com/wcc/itransaction";
		}

		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			string remoteInstId = payment.PaymentMethod.DynamicProperty<string>().RemoteInstId;
			string authPw = payment.PaymentMethod.DynamicProperty<string>().AuthPW;
			bool testMode = payment.PaymentMethod.DynamicProperty<bool>().TestMode;

			var dict = new Dictionary<string, string>();
			dict.Add("instId", remoteInstId);
			dict.Add("op", "postAuth-full");
			dict.Add("authPW", authPw);
			dict.Add("authMode", "O");

			if (testMode)
				dict.Add("testMode", "100");

			dict.Add("transId", payment.TransactionId);

			var httpPost = new HttpPost(GetPostUrl(payment.PaymentMethod), dict);

			string message = httpPost.GetString();

			bool callStatus = GetCallStatus(message, payment.TransactionId);

			if (callStatus)
				status = PaymentMessages.AcquireSuccess + " >> " + message;
			else
				status = PaymentMessages.AcquireFailed + " >> " + message;

			return callStatus;
		}

		private static bool GetCallStatus(string message, string transactionId)
		{
			return !string.IsNullOrEmpty(message) && message.StartsWith("A") && message.Contains(transactionId) && message.Contains("postproc.msg.queued");
		}

		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			string remoteInstId = payment.PaymentMethod.DynamicProperty<string>().RemoteInstId;
			string authPw = payment.PaymentMethod.DynamicProperty<string>().AuthPW;
			bool testMode = payment.PaymentMethod.DynamicProperty<bool>().TestMode;

			var dict = new Dictionary<string, string>();
			dict.Add("authPW", authPw);
			dict.Add("instId", remoteInstId);
			dict.Add("cartId", payment.ReferenceId);
			dict.Add("op", "refund-full");

			if (testMode)
				dict.Add("testMode", "100");

			dict.Add("transId", payment.TransactionId);

			var httpPost = new HttpPost(GetPostUrl(payment.PaymentMethod), dict);

			string message = httpPost.GetString();

			bool callStatus = GetCallStatus(message, payment.TransactionId);

			if (callStatus)
				status = PaymentMessages.RefundSuccess + " >> " + message;
			else
				status = PaymentMessages.RefundFailed + " >> " + message;

			return callStatus;
		}
	}
}
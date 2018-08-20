using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using AuthorizeNet;
using UCommerce.Content;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Infrastructure.Environment;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.Configuration;

namespace UCommerce.Transactions.Payments.Dibs
{
	/// <summary>
	/// Implementation of the http://dibs.dk payment provider.
	/// </summary>
	public class DibsPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IWebRuntimeInspector _webRuntimeInspector;
	    private readonly ILoggingService _loggingService;
		private DibsMd5Computer DibsMd5Computer { get; set; }
		private AbstractPageBuilder PageBuilder { get; set; }

		public DibsPaymentMethodService(DibsPageBuilder pageBuilder, DibsMd5Computer md5Computer, IWebRuntimeInspector webRuntimeInspector, ILoggingService loggingService)
		{
			_webRuntimeInspector = webRuntimeInspector;
		    _loggingService = loggingService;
			DibsMd5Computer = md5Computer;
			PageBuilder = pageBuilder;
		}

		/// <summary>
		/// Renders the page with the information needed by the payment provider.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>The html rendered.</returns>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		/// <summary>
		/// Processes the callback and excecutes a pipeline if there is one specified for this paymentmethodservice.
		/// </summary>
		/// <param name="payment">The payment to process.</param>
		public override void ProcessCallback(Payment payment)
		{
			Guard.Against.MissingHttpContext(_webRuntimeInspector);
			Guard.Against.MissingRequestParameter("transact");
			Guard.Against.PaymentNotPendingAuthorization(payment);

			string transactionId = GetTransactionParameter();
			
			var hashVeryfied = VerifyMd5Hash(payment);

			payment.TransactionId = transactionId;
			
			if (hashVeryfied)
			{
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}
			else
			{
                _loggingService.Log<DibsPaymentMethodService>("Hash was not verified");
                payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Declined);
				payment.Save(); //Save payment to ensure transactionId not lost.
			}
		}

		private bool VerifyMd5Hash(Payment payment)
		{
			bool useMd5 = payment.PaymentMethod.DynamicProperty<bool>().UseMd5;
			if (!useMd5) return true;

			string key1 = payment.PaymentMethod.DynamicProperty<string>().Key1.ToString();
			string key2 = payment.PaymentMethod.DynamicProperty<string>().Key2.ToString();
			string transact = GetTransactionParameter();

			const string format = "When using md5 \"{0}\" cannot be null or empty";

			var authKeyParameter = GetParameter("authkey", format);
			var currencyParameter = GetParameter("currency", format);
			var amountParameter = GetParameter("amount", format);

			var currencyCodeTranslater = new CurrencyCodeTranslater();
			int isoCode = currencyCodeTranslater.FromIsoCode(currencyParameter);

			var hashComputer = new DibsMd5Computer();
			var md5ResponseKey = hashComputer.GetPostMd5Key(transact, amountParameter, isoCode, key1, key2);


		    var verifyMd5Hash = authKeyParameter.Equals(md5ResponseKey);
		    if (!verifyMd5Hash)
		    {
		        _loggingService.Log<DibsPaymentMethodService>(string.Format("Comparing response authkey: '{0}' with calculated key: '{1}' returned false. Hash cannot be verified!", authKeyParameter, md5ResponseKey));
		    }

		    return verifyMd5Hash;
		}

		private string GetTransactionParameter()
		{
			return HttpContext.Current.Request["transact"];
		}

		private string GetCancelUrl(string merchant, string orderid, string transact, string md5key)
		{
			const string format = "https://payment.architrade.com/cgi-adm/cancel.cgi?merchant={0}&orderid={1}&transact={2}&md5key={3}&textreply=yes";
			return string.Format(format, merchant, orderid, transact, md5key);
		}

		private string GetRefundUrl(string merchant, string amount, string currency, string transact, string orderid, string md5Key)
		{
			const string format = "https://payment.architrade.com/cgi-adm/refund.cgi?merchant={0}&amount={1}&currency={2}&transact={3}&orderid={4}&md5key={5}&textreply=yes";
			return string.Format(format, merchant, amount, currency, transact, orderid, md5Key);
		}


		/// <summary>
		/// Gets the status.
		/// </summary>
		/// <param name="input">The input where the status is extracted from.</param>
		/// <returns></returns>
		protected virtual bool GetStatus(string input)
		{
			var reg = new Regex("status=([A-z]+)", RegexOptions.IgnoreCase);
			Match match = reg.Match(input);
			if (!match.Success)
				return false;

			string value = match.Groups[1].Value;
			if (string.IsNullOrEmpty(value))
				return false;

		    return string.Equals("ACCEPTED", value, StringComparison.InvariantCultureIgnoreCase);
		}

		private string GetCaptureUrl(string merchant, string amount, string transact, string orderId, string key)
		{
			const string format = "https://payment.architrade.com/cgi-bin/capture.cgi?merchant={0}&amount={1}&transact={2}&orderid={3}&textreply=true&md5key={4}";
			return string.Format(format, merchant, amount, transact, orderId, key);
		}

		/// <summary>
		/// Gets the error message.
		/// </summary>
		/// <param name="errorMessage">The error message.</param>
		/// <returns></returns>
		protected virtual string GetErrorMessage(string errorMessage)
		{
			int errorNo = -1;
			Match match = Regex.Match(errorMessage, @"result=(\d{1,})", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				errorNo = int.Parse(match.Groups[1].Value);
			}

			switch (errorNo)
			{
				case 0:
					return "OK";
				case 1:
					return "No response from acquirer.";
				case 2:
					return "Error in the parameters sent to the DIBS server. An additional parameter called message is returned, with a value that may help identifying the error.";
				case 3:
					return "Credit card expired.";
				case 4:
					return "Rejected by acquirer.";
				case 5:
					return "Authorisation older than7 days.";
				case 6:
					return "Transaction status on the DIBS server does not allow capture.";
				case 7:
					return "Amount too high.";
				case 8:
					return "Amount is zero.";
				case 9:
					return "Order number (orderid) does not correspond to the authorisation order number.";
				case 10:
					return "Re-authorisation of the transaction was rejected.";
				case 11:
					return "Not able to communicate with the acquier.";
				case 12:
					return "Confirm request error.";
				case 14:
					return "Capture is called for a transaction which is pending for batch - i.e. capture was already called.";
				case 15:
					return "Capture was blocked by DIBS.";
				default:
					return string.Format("Unknown. Code: {0}", errorNo);
			}
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();
			string key1 = payment.PaymentMethod.DynamicProperty<string>().Key1.ToString();
			string key2 = payment.PaymentMethod.DynamicProperty<string>().Key2.ToString();
			string login = payment.PaymentMethod.DynamicProperty<string>().Login.ToString();
			string password = payment.PaymentMethod.DynamicProperty<string>().Password.ToString();
			
			string amount = payment.Amount.ToCents().ToString();
			var referenceId = payment.ReferenceId;
			var transactionId = payment.TransactionId;

			string key = DibsMd5Computer.GetCaptureMd5Key(referenceId, transactionId, amount, key1, key2, merchant);

			string url = GetCaptureUrl(merchant, amount, transactionId, referenceId, key);

			var httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);
			httpWebRequest.Credentials = new NetworkCredential(login, password);

			WebResponse webResponse;
			try
			{
				webResponse = httpWebRequest.GetResponse();
			}
			catch (WebException exception)
			{
				var httpWebResponse = exception.Response as HttpWebResponse;
				if (httpWebResponse != null)
				{
					if (httpWebResponse.StatusCode == HttpStatusCode.Unauthorized)
					{
						status = exception.Message;
						return false;
					}
				}

				throw;
			}

			var streamReader = new StreamReader(webResponse.GetResponseStream());
			string errorStatus = streamReader.ReadToEnd();

			bool dibsResponseStatus = GetStatus(errorStatus);

			if(dibsResponseStatus)
				status = PaymentMessages.AcquireSuccess + " >> " + GetErrorMessage(errorStatus);
			else
				status = PaymentMessages.AcquireFailed + " >> " + GetErrorMessage(errorStatus);

			return dibsResponseStatus;
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool  RefundPaymentInternal(Payment payment, out string status)
		{
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();
			string key1 = payment.PaymentMethod.DynamicProperty<string>().Key1.ToString();
			string key2 = payment.PaymentMethod.DynamicProperty<string>().Key2.ToString();
			string login = payment.PaymentMethod.DynamicProperty<string>().Login.ToString();
			string password = payment.PaymentMethod.DynamicProperty<string>().Password.ToString();

			var amount = payment.Amount.ToCents().ToString();

			var transactionId = payment.TransactionId;
			var referenceId = payment.ReferenceId;
			var key = DibsMd5Computer.GetRefundKey(referenceId, transactionId, amount, key1, key2, merchant);

			var isoCode = payment.PurchaseOrder.BillingCurrency.ISOCode;
			var refundUrl = GetRefundUrl(merchant, amount, isoCode, transactionId, referenceId, key);

			var httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(refundUrl);
			httpWebRequest.Credentials = new NetworkCredential(login, password);

			WebResponse webResponse;
			try
			{
				webResponse = httpWebRequest.GetResponse();
			}
			catch (WebException exception)
			{
				var httpWebResponse = exception.Response as HttpWebResponse;
				if (httpWebResponse != null)
				{
					if (httpWebResponse.StatusCode == HttpStatusCode.Unauthorized)
					{
						status = exception.Message;
						return false;
					}
				}

				throw;
			}

			var streamReader = new StreamReader(webResponse.GetResponseStream());
			string errorStatus = streamReader.ReadToEnd();

			var dibsResponseStatus = GetStatus(errorStatus);

			if(dibsResponseStatus)
				status = PaymentMessages.RefundSuccess + " >> " + GetErrorMessage(errorStatus);
			else
				status = PaymentMessages.RefundFailed + " >> " + GetErrorMessage(errorStatus);

			return dibsResponseStatus;
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();
			string key1 = payment.PaymentMethod.DynamicProperty<string>().Key1.ToString();
			string key2 = payment.PaymentMethod.DynamicProperty<string>().Key2.ToString();
			string login = payment.PaymentMethod.DynamicProperty<string>().Login.ToString();
			string password = payment.PaymentMethod.DynamicProperty<string>().Password.ToString();

			var referenceId = payment.ReferenceId;
			var key = DibsMd5Computer.GetCancelMd5Key(referenceId, payment.TransactionId, key1, key2, merchant);

			var refundUrl = GetCancelUrl(merchant, referenceId, payment.TransactionId, key);

			var httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(refundUrl);
			httpWebRequest.Credentials = new NetworkCredential(login, password);

			WebResponse webResponse;
			try
			{
				webResponse = httpWebRequest.GetResponse();
			}
			catch (WebException exception)
			{
				var httpWebResponse = exception.Response as HttpWebResponse;
				if (httpWebResponse != null)
				{
					if (httpWebResponse.StatusCode == HttpStatusCode.Unauthorized)
					{
						status = exception.Message;
						return false;
					}
				}

				throw;
			}

			var streamReader = new StreamReader(webResponse.GetResponseStream());
			string errorStatus = streamReader.ReadToEnd();

			bool dibsResponseStatus = GetStatus(errorStatus);

			status = GetErrorMessage(errorStatus);

			return dibsResponseStatus;
		}
	}
}

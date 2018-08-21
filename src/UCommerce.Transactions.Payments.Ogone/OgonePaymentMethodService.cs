using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Web;
using System.Xml.Linq;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Infrastructure.Environment;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.Configuration;
namespace UCommerce.Transactions.Payments.Ogone
{
	/// <summary>
	/// Implementation of the Ogone Payment provider.
	/// The implementation supports Authorize, Acquire, and Cancel.
	/// </summary>
	/// <remarks></remarks>
	public class OgonePaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IWebRuntimeInspector _webRuntimeInspector;
		private OgonePageBuilder OgonePageBuilder { get; set; }
		
		public OgonePaymentMethodService(OgonePageBuilder oGonePageBuilder, IWebRuntimeInspector webRuntimeInspector)
		{
			_webRuntimeInspector = webRuntimeInspector;
			OgonePageBuilder = oGonePageBuilder;
		}

		/// <summary>
		/// Method is called when the order is being completed by the merchant when the <see cref="Payment"/> is authorized. 
		/// </summary>
		/// <param name="payment">The authorized payment.</param>
		/// <param name="status">The status.</param>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			var paymentMethod = payment.PaymentMethod;
			string pspId = paymentMethod.DynamicProperty<string>().PspId;
			string password = paymentMethod.DynamicProperty<string>().Password;
			string userId = paymentMethod.DynamicProperty<string>().UserId;
			string shaSignIn = paymentMethod.DynamicProperty<string>().ShaSignIn;
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;


			int ogonePaymentStatus = RequestPaymentStatusAtOgone(payment, pspId, userId, password, testMode);
			if (ogonePaymentStatus != 5)
			{
				status = string.Format("Expected status was 'Payment Authorized' (5) but was {0}. {1}. Check STATUS message at {2}.",
					 ogonePaymentStatus,
					 "Processing a payment request takes time. Please wait for Ogone to finalize the authorization.",
					 "https://secure.ogone.com/ncol/param_cookbook.asp"
				);
				return false;
			}

			var dict = new Dictionary<string, string>();
			dict.Add("amount", payment.Amount.ToCents().ToString());
			dict.Add("currency", payment.PurchaseOrder.BillingCurrency.ISOCode);
			dict.Add("operation", "SAS");
			dict.Add("orderid", payment.ReferenceId);
			dict.Add("payid", payment.TransactionId);
			dict.Add("pspid", pspId);
			dict.Add("pswd", password);
			dict.Add("userid", userId);
			
			var shaComputer = new OgoneSha1Computer();
			var acquirePaymentShaSignIn = new AcquirePaymentShaSignIn();
			dict.Add("SHASign", shaComputer.ComputeHash(acquirePaymentShaSignIn.BuildHashString(dict, shaSignIn)).ToUpper());

			var url = GetMaintenanceDirectUrl(testMode);
			
			var oGoneDataCapture = RequestOgone(url,dict);

			var ncresponse = oGoneDataCapture.Descendants("ncresponse").Single();
			var maintenanceStatus = Convert.ToInt32(ncresponse.Attributes("STATUS").Single().Value);
			payment.PaymentStatus = PaymentStatus.Get((int)ConvertAcquireResultToPaymentStatus(maintenanceStatus));
			
			status = "";
			
			if (payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.AcquireFailed)
			{
				status = GetNCResponseErrorMessage(ncresponse);
				return false;
			}
				
			return true;
		}

		/// <summary>
		/// Method is called when the order is being canceled by the merchant before the <see cref="Payment"/> is acquired.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			var paymentMethod = payment.PaymentMethod;
			string pspId = paymentMethod.DynamicProperty<string>().PspId;
			string password = paymentMethod.DynamicProperty<string>().Password;
			string userId = paymentMethod.DynamicProperty<string>().UserId;
			string shaSignIn = paymentMethod.DynamicProperty<string>().ShaSignIn;
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;

			int ogonePaymentStatus = RequestPaymentStatusAtOgone(payment, pspId, userId, password, testMode);
			if (ogonePaymentStatus == 9 || ogonePaymentStatus == 91)
				return RefundPaymentInternal(payment, out status);
			if (ogonePaymentStatus != 5)
			{
				status = string.Format("expected status was 5 or 91 for cancel payment, but was {0}. Check STATUS message at {1}"
					, ogonePaymentStatus
					, "https://secure.ogone.com/ncol/param_cookbook.asp?CSRFSP=%2Fncol%2Ftest%2Fdownload_docs%2Easp&CSRFKEY=7E003EFC9703DF1A30BF28559ED87B534C0F0309&CSRFTS=20110822081113"
				);
				return false;
			}

			var dict = new Dictionary<string, string>();
			dict.Add("amount", payment.Amount.ToCents().ToString());
			dict.Add("currency", payment.PurchaseOrder.BillingCurrency.ISOCode);
			dict.Add("operation", "DES");
			dict.Add("orderid", payment.PurchaseOrder.OrderId.ToString());
			dict.Add("payid", payment.TransactionId);
			dict.Add("pspid", pspId);
			dict.Add("pswd", password);
			dict.Add("userid", userId);

			var shaComputer = new OgoneSha1Computer();
			var cancelPaymentShaSignIn = new CancelPaymentShaSignIn();
			dict.Add("SHASign", shaComputer.ComputeHash(cancelPaymentShaSignIn.BuildHashString(dict, shaSignIn)).ToUpper());

			var url = GetMaintenanceDirectUrl(testMode);

			var oGoneDataCapture = RequestOgone(url, dict);

			var ncresponse = oGoneDataCapture.Descendants("ncresponse").Single();
			var maintenanceStatus = Convert.ToInt32(ncresponse.Attributes("STATUS").Single().Value);
			payment.PaymentStatus = PaymentStatus.Get((int)ConvertCancelResultToPaymentStatus(maintenanceStatus));
			status = "";
			if (payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.Declined)
			{
				status = GetNCResponseErrorMessage(ncresponse);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Processes the callback from Ogone after the customer has entered credit card information and clicked submit.
		/// </summary>
		/// <param name="payment">The payment.</param>
		public override void ProcessCallback(Payment payment)
		{
			Guard.Against.PaymentNotPendingAuthorization(payment);
			Guard.Against.MissingHttpContext(_webRuntimeInspector);
			Guard.Against.MissingRequestParameter("STATUS");
			Guard.Against.MissingRequestParameter("PAYID");
			Guard.Against.MissingRequestParameter("SHASIGN");
			
			var paymentMethod = payment.PaymentMethod;
			HttpContext context = HttpContext.Current;
			var computer = new OgoneSha1Computer();
			var hashString = new ProcessCallBackShaSignOut();

			string shaSignOut = paymentMethod.DynamicProperty<string>().ShaSignOut;

			string statusCode = context.Request["STATUS"];
			string transactionId = context.Request["PAYID"];
			string oGoneShaSign = context.Request["SHASIGN"].ToUpper();
			
			string stringToHash = hashString.BuildHashString(context, shaSignOut);
			string shaSign = computer.ComputeHash(stringToHash).ToUpper();
			if (oGoneShaSign != shaSign)
				throw new SecurityException("Shasigns did not match. Please make sure that 'SHA-OUT Pass phrase' is configured in Ogone and that the same key is configured in Ogone.config.");

			payment["response"] = context.Request.Url.ToString();
			
			PaymentStatusCode paymentStatus = ConvertAuthorizeResultToPaymentStatus(Convert.ToInt32(statusCode));
			payment.TransactionId = transactionId;
			payment.PaymentStatus = PaymentStatus.Get((int) paymentStatus);

			if (PaymentIsDeclined(payment))
			{
				payment.Save();
			}
			else
			{
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));						
			}
		}

		private bool PaymentIsDeclined(Payment payment)
		{
			return payment.PaymentStatus.PaymentStatusId == (int) PaymentStatusCode.Declined;
		}

		/// <summary>
		/// Method is called when the order is being canceled by the merchant, after the <see cref="Payment"/> have been acquired by the merchant.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			var paymentMethod = payment.PaymentMethod;
			string pspId = paymentMethod.DynamicProperty<string>().PspId;
			string password = paymentMethod.DynamicProperty<string>().Password;
			string userId = paymentMethod.DynamicProperty<string>().UserId;
			string shaSignIn = paymentMethod.DynamicProperty<string>().ShaSignIn;
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;

			int paymentStatus = RequestPaymentStatusAtOgone(payment, pspId, userId, password, testMode);
			if (paymentStatus != 9 && paymentStatus != 91)
			{
				
				status = string.Format("Expected status was 'Payment requested' (9) or 'Payment Processig' (91) for refund, but was {0}. Check 'STATUS' message at {1}",
					paymentStatus,
					"https://secure.ogone.com/ncol/param_cookbook.asp"
				);
				return false;
			}
			var dict = new Dictionary<string, string>();

			dict.Add("amount", payment.Amount.ToCents().ToString());
			dict.Add("currency", payment.PurchaseOrder.BillingCurrency.ISOCode);
			dict.Add("operation", "RFS");
			dict.Add("orderid", payment.ReferenceId);
			dict.Add("payid", payment.TransactionId);
			dict.Add("pspid", pspId);
			dict.Add("pswd", password);
			dict.Add("userid", userId);

			var shaComputer = new OgoneSha1Computer();
			var hashString = new CancelPaymentShaSignIn();

			dict.Add("SHASign", shaComputer.ComputeHash(hashString.BuildHashString(dict, shaSignIn)).ToUpper());

			var url = GetMaintenanceDirectUrl(testMode);

			var oGoneDataCapture = RequestOgone(url, dict);
			var ncresponse = oGoneDataCapture.Descendants("ncresponse").Single();
			
			var maintenanceStatus = Convert.ToInt32(ncresponse.Attributes("STATUS").Single().Value);
			payment.PaymentStatus = PaymentStatus.Get((int)ConvertRefundResultToPaymentStatus(maintenanceStatus));
			
			status = "";
			if (payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.Declined)
			{
				status = GetNCResponseErrorMessage(ncresponse);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Renders the page.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return OgonePageBuilder.Build(paymentRequest);
		}

		/// <summary>
		/// Gets the payment status code of the callback. 
		/// Method is used in order to tell the system weither the payment request
		/// is authorized, acquired or declined. Return status has to be either "authorized" (5) or "acquired" (9) for not to be declined.
		/// </summary>
		/// <param name="status">The status.</param>
		/// <returns>The PaymentStatusCode to set equivilent to the status set at Ogone.</returns>
		private PaymentStatusCode ConvertAuthorizeResultToPaymentStatus(int status)
		{
			switch (status)
			{
				case 5:
					return PaymentStatusCode.Authorized;
				case 9:
					return PaymentStatusCode.Acquired;
				default:
					return PaymentStatusCode.Declined;
			}
		}

		/// <summary>
		/// Gets the status of the response when doing acquire on the order.
		/// Method is used in order to tell the system weither the acquire is succeful or failed.
		/// </summary>
		/// <param name="status">The status.</param>
		/// <returns>The PaymentStatusCode to set equivilent to the status set at Ogone.</returns>
		private PaymentStatusCode ConvertAcquireResultToPaymentStatus(int status)
		{
			switch (status)
			{
				case 9:
					return PaymentStatusCode.Acquired;
				case 91:
					return PaymentStatusCode.Acquired;
				default:
					return PaymentStatusCode.AcquireFailed;
			}
		}

		/// <summary>
		/// Gets the status of the response when doing a cancel payment.
		/// Method is used in order to tell the system weither the cancel payment was accepted or declined. 
		/// </summary>
		/// <param name="status">The status.</param>
		/// <returns>The PaymentStatusCode to set equivilent to the status set at Ogone.</returns>
		private PaymentStatusCode ConvertCancelResultToPaymentStatus(int status)
		{
			switch (status)
			{
				case 6:
					return PaymentStatusCode.Cancelled;
				case 61:
					return PaymentStatusCode.Cancelled;
				default:
					return PaymentStatusCode.Declined;
			}
		}

		/// <summary>
		/// Gets the status of the response when doing a refund on the <see cref="Payment"/>.
		/// Method is used in order to tell the system weither the refund was accepted or declined. 
		/// </summary>
		/// <param name="status">The status.</param>
		/// <returns>The PaymentStatusCode to set equivilent to the status set at Ogone.</returns>
		private PaymentStatusCode ConvertRefundResultToPaymentStatus(int status)
		{
			switch (status)
			{
				case 8:
					return PaymentStatusCode.Refunded;
				case 81:
					return PaymentStatusCode.Refunded;
				default:
					return PaymentStatusCode.Declined;
			}
		}

		protected virtual int RequestPaymentStatusAtOgone(Payment payment, string pspId, string userId, string password, bool testMode)
		{
			var dict = new Dictionary<string, string>();
			dict.Add("pspid", pspId);
			dict.Add("userid", userId);
			dict.Add("pswd", password);
			dict.Add("payid", payment.TransactionId);
			
			var url = testMode ? "https://secure.ogone.com/ncol/test/querydirect.asp" : "https://secure.ogone.com/ncol/prod/querydirect.asp";		
			
			var oGoneDataCapture = RequestOgone(url, dict);

			var ncresponse = oGoneDataCapture.Descendants("ncresponse").Single();

			return Convert.ToInt32(ncresponse.Attributes("STATUS").Single().Value);
		}

		/// <summary>
		/// Requests the Ogone paymentprovider.
		/// </summary>
		/// <param name="url">The URL.</param>
		/// <param name="formValuesToPost">The form values to post.</param>
		/// <returns>The result of the request. Return format is XML.</returns>
		private XDocument RequestOgone(string url, IDictionary<string, string> formValuesToPost)
		{
			url += AddQueryString(formValuesToPost);
			var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
			httpWebRequest.Method = "GET";
			string stream = new StreamReader(httpWebRequest.GetResponse().GetResponseStream(), Encoding.GetEncoding("utf-8")).ReadToEnd();

			return XDocument.Parse(stream);
		}

		/// <summary>
		/// Appends the query string to the URL.
		/// </summary>
		/// <param name="dictionary">The dictionary of key/values to attach to the URL.</param>
		/// <returns></returns>
		/// <remarks></remarks>
		private string AddQueryString(IDictionary<string, string> dictionary)
		{
			return "?" + string.Join("&", (dictionary).Select((a => a.Key + "=" + a.Value)).ToArray());
		}

		/// <summary>
		/// Gets the maintenance direct URL for cancel, acquire and refund payment.
		/// 
		/// </summary>
		/// <returns>The url either in testmode or production mode.</returns>
		/// <remarks></remarks>
		private string GetMaintenanceDirectUrl(bool testMode)
		{
			return testMode ? "https://secure.ogone.com/ncol/test/maintenancedirect.asp" : "https://secure.ogone.com/ncol/prod/maintenancedirect.asp";
		}

		/// <summary>
		/// Gets the errorCode and errorMessage of the ncresponse.
		/// This method is called when a maitenance or payment request fails
		/// </summary>
		/// <param name="ncresponse">The XElement that contains attributes: "NCERROR" and "NCERRORPLUS".</param>
		/// <returns>A string containing the error code with the error message</returns>
		/// <remarks></remarks>
		private string GetNCResponseErrorMessage(XElement ncresponse)
		{
			var errorCode = ncresponse.Attributes("NCERROR").Single().Value;
			var errorCodePlus = ncresponse.Attributes("NCERRORPLUS").Single().Value;
			return string.Format("Error: {0} Error message: {1}", errorCode, errorCodePlus);
		}
	}
}

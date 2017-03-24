using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.EPay.EPayBackendService;

namespace UCommerce.Transactions.Payments.EPay
{
	/// <summary>
	/// Implementation of the http://epay.dk payment provider
	/// </summary>
	public class EPayPaymentMethodService : ExternalPaymentMethodService
	{
		private CommerceConfigurationProvider ConfigurationProvider { get; set; }
		private AbstractPageBuilder PageBuilder { get; set; }
		private EPayMd5Computer Md5Computer { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="EPayPaymentMethodService"/> class.
		/// </summary>
		public EPayPaymentMethodService(CommerceConfigurationProvider configProvider, EPayPageBuilder pageBuilder, EPayMd5Computer md5Computer)
		{
			ConfigurationProvider = configProvider;
			PageBuilder = pageBuilder;
			Md5Computer = md5Computer;
			Language = 2;

			var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
			{
				CloseTimeout = TimeSpan.FromMinutes(1),
				OpenTimeout = TimeSpan.FromMinutes(1),
				ReceiveTimeout = TimeSpan.FromMinutes(10),
				SendTimeout = TimeSpan.FromMinutes(10),
				AllowCookies = false,
				BypassProxyOnLocal = false,
				HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
				MaxBufferSize = 65536,
				MaxBufferPoolSize = 524288,
				MaxReceivedMessageSize = 65536,
				MessageEncoding = WSMessageEncoding.Text,
				TextEncoding = Encoding.UTF8,
				TransferMode = TransferMode.Buffered,
				UseDefaultWebProxy = true
			};

			var uri = new Uri("https://ssl.ditonlinebetalingssystem.dk/remote/payment.asmx", UriKind.Absolute);
			var endpointAddress = new EndpointAddress(uri);

			Client = new PaymentSoapClient(binding, endpointAddress);
		}
		/// <summary>
		/// Gets the PBS error.
		/// </summary>
		/// <param name="errorNumber">The error number.</param>
		/// <param name="paymentMethod">The payment method. Needed to get configuration data</param>
		/// <returns></returns>
		protected virtual string GetPbsError(int errorNumber, PaymentMethod paymentMethod)
		{
			string errorMessage = null;
			int errorCode = 0;
			if (!Client.getPbsError(int.Parse(paymentMethod.DynamicProperty<string>().MerchantNumber), Language, errorCode, paymentMethod.DynamicProperty<string>().Pwd, ref errorMessage, ref errorCode))
			{
				errorMessage = string.Format("Tried to lookup the error but failed with code: {0}", errorCode);
			}
			return errorMessage;
		}

		/// <summary>
		/// Gets the Epay error.
		/// </summary>
		/// <param name="errorNumber">The error number.</param>
		/// <param name="paymentMethod">The payment method. Needed to get configuration data</param>
		/// <returns></returns>
		protected virtual string GetEpayError(int errorNumber, PaymentMethod paymentMethod)
		{
			string errorMessage = null;
			int errorCode = 0;
			if (!Client.getEpayError(int.Parse(paymentMethod.DynamicProperty<string>().MerchantNumber), Language, errorNumber, paymentMethod.DynamicProperty<string>().Pwd, ref errorMessage, ref errorCode))
			{
				errorMessage = string.Format("Tried to lookup the error code: {0} but failed with code: {1}", errorNumber, errorCode);
			}
			return errorMessage;
		}

		/// <summary>
		/// Gets the error from PBS and Epay and joins them into a string.
		/// </summary>
		/// <param name="pbsErrorNumber">The PBS error number.</param>
		/// <param name="epayErrorNumber">The epay error number.</param>
		/// <param name="paymentMethod">The payment method. Needed to get configuration data</param>
		/// <returns>A string containng the errors</returns>
		private string GetError(int pbsErrorNumber, int epayErrorNumber, PaymentMethod paymentMethod)
		{
			var pbsError = GetPbsError(pbsErrorNumber, paymentMethod);
			var epayError = GetEpayError(epayErrorNumber, paymentMethod);

			return string.Format("Epay: {0}, Pbs: {1}", epayError, pbsError);
		}

		/// <summary>
		/// Gets or sets the language.
		/// </summary>
		/// <value>The language.</value>
		public virtual int Language { get; private set; }

		private PaymentSoapClient Client { get; set; }

		/// <summary>
		/// Renders the forms to be submitted to the payment provider.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns>A string containing the html form.</returns>
		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		/// <summary>
		/// Processed the callback received from the payment provider.
		/// </summary>
		/// <param name="payment">The payment.</param>
		public override void ProcessCallback(Payment payment)
		{
			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
				return;

			int transactionid = GetTransactionIdFromHttpRequestThrowsExceptionIfNotInt();

			var paymentStatus = PaymentStatusCode.Authorized;

			if (payment.PaymentMethod.DynamicProperty<bool>().UseMd5)
			{
				var parameters = new Dictionary<string, string>();
				foreach (var key in HttpContext.Current.Request.QueryString.AllKeys.Where(x => x != "hash"))
				{
					parameters.Add(key, HttpContext.Current.Request.QueryString[key]);
				}

				string md5Key = payment.PaymentMethod.DynamicProperty<string>().Key;
				var calculatedMd5Key = Md5Computer.GetPreMd5Key(parameters, md5Key);

				const string format = "When using md5 \"{0}\" cannot be null or empty.";
				var md5ReceivedFromEPay = GetParameter("hash", format);

				if (!md5ReceivedFromEPay.Equals(calculatedMd5Key))
					paymentStatus = PaymentStatusCode.Declined;
			}

			payment.PaymentStatus = PaymentStatus.Get((int)paymentStatus);
			payment.TransactionId = transactionid.ToString();
			ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
		}

		private int GetTransactionIdFromHttpRequestThrowsExceptionIfNotInt()
		{
			string transactParameter = HttpContext.Current.Request["txnid"];
			if (string.IsNullOrEmpty(transactParameter))
				throw new ArgumentException(@"txnid must be present in query string.");

			int transactionid;
			if (!int.TryParse(transactParameter, out transactionid))
				throw new FormatException(@"txnid must be a valid int32.");
			return transactionid;
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			Int64 transactionId;
			if (!Int64.TryParse(payment.TransactionId, out transactionId))
				throw new FormatException(string.Format("Can't convert: {0} to a System.Int64", (object)payment.TransactionId));

			int amount = payment.Amount.ToCents();

			int epayResponse = 0;
			int pbsResponse = 0;
			if (!Client.capture(int.Parse(payment.PaymentMethod.DynamicProperty<string>().MerchantNumber), transactionId, amount, "", payment.PaymentMethod.DynamicProperty<string>().Pwd, ref pbsResponse, ref epayResponse))
			{
				status = PaymentMessages.AcquireFailed + " >> " + GetError(pbsResponse, epayResponse, payment.PaymentMethod);
				return false;
			}

			status = PaymentMessages.AcquireSuccess;
			return true;
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			Int64 transactionId;
			if (!Int64.TryParse(payment.TransactionId, out transactionId))
				throw new FormatException(string.Format("Can't convert: {0} to a System.Int64", (object)payment.TransactionId));

			int amount = payment.Amount.ToCents();

			int epayResponse = 0;
			int pbsResponse = 0;
			if (!Client.credit(int.Parse(payment.PaymentMethod.DynamicProperty<string>().MerchantNumber), transactionId, amount, "", payment.PaymentMethod.DynamicProperty<string>().Pwd, ref pbsResponse, ref epayResponse))
			{
				// ERROR ... 
				status = PaymentMessages.RefundFailed + " >> " + GetError(pbsResponse, epayResponse, payment.PaymentMethod);
				return false;
			}

			status = PaymentMessages.RefundSuccess;
			return true;
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			Int64 transactionId;
			if (!Int64.TryParse(payment.TransactionId, out transactionId))
				throw new FormatException(string.Format("Can't convert: {0} to a System.Int64", (object)payment.TransactionId));

			int epayResponse = 0;
			if (!Client.delete(int.Parse(payment.PaymentMethod.DynamicProperty<string>().MerchantNumber), transactionId, "", payment.PaymentMethod.DynamicProperty<string>().Pwd, ref epayResponse))
			{
				// ERROR ... 
				status = PaymentMessages.CancelFailed + " >> " + string.Format("Epay: {0}", GetEpayError(epayResponse, payment.PaymentMethod));
				return false;
			}

			status = PaymentMessages.CancelSuccess;
			return true;
		}
	}
}

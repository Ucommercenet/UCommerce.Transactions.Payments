using System;
using System.Net;
using System.Text;
using System.Web;
using AuthorizeNet;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Authorizedotnet
{
	/// <summary>
	/// Authorize.Net integration using SIM APIs.
	/// </summary>
    public class AuthorizedotnetPaymentMethodService : ExternalPaymentMethodService
    {
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private AuthorizedotnetPageBuilder AuthorizedotnetPageBuilder { get; set; }

        public AuthorizedotnetPaymentMethodService(AuthorizedotnetPageBuilder pageBuilder, IAbsoluteUrlService absoluteUrlService)
		{
	        _absoluteUrlService = absoluteUrlService;
	        AuthorizedotnetPageBuilder = pageBuilder;
		}

		/// <summary>
		/// Renders the page to post at Authorize.NET using <see cref="AuthorizedotnetPageBuilder"/>.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns></returns>
		/// <remarks></remarks>
        public override string RenderPage(PaymentRequest paymentRequest)
        {
            return AuthorizedotnetPageBuilder.Build(paymentRequest);
        }

		/// <summary>
		/// Requests the payment.
		/// </summary>
		/// <param name="paymentRequest">The payment request.</param>
		/// <returns></returns>
		/// <remarks>Authroize.NET only supports USD which this method secures by checking the ISO code and throws an exception.</remarks>
        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {

            string currencyIsoCode = paymentRequest.Amount.Currency.ISOCode;
            if (currencyIsoCode.ToLower() != "usd")
                throw new InvalidOperationException(string.Format("Authorize.Net doesn't support {0} as currency, only USD is supported. To use Authorize.Net please change PaymentRequest to use USD.", currencyIsoCode));

            return base.RequestPayment(paymentRequest);
        }

		/// <summary>
		/// Processes the callback from Authorize.NET after a customer authorizes a payment request.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <remarks>Method communicates with Authorize.NET server and processes the auth_codes from the response.
		/// Normally the callback would do a redirect at the end.
		/// Instead we write the page directly to them.
		/// </remarks>
        public override void ProcessCallback(Payment payment)
        {
            if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
                return;

			var paymentStatus = PaymentStatusCode.Declined;

            var responseCodeParameter = GetParameter("x_response_code", "\"{0}\" cannot be null or empty");
            var responseReasonCodeParameter = GetParameter("x_response_reason_code", "\"{0}\" cannot be null or empty");
            var responseReasonTextParameter = GetParameter("x_response_reason_text", "\"{0}\" cannot be null or empty");
			
            string transactParameter = HttpContext.Current.Request["x_trans_id"];
            if (string.IsNullOrEmpty(transactParameter))
                throw new ArgumentException(@"transact must be present in query string.");

			string transact = transactParameter;

			// If payment received OK, proceed with processing
			if (responseCodeParameter == "1")
			{
				const string format = "When using md5 \"{0}\" cannot be null or empty";
				payment["auth_code"] = GetParameter("x_auth_code", format);
				
				var md5KeyParameter = GetParameter("x_MD5_Hash", format).ToLower();
				var amountParameter = GetParameter("x_amount", format);

				// Configuration values
				string md5Hash = payment.PaymentMethod.DynamicProperty<string>().Md5Hash;
				string apiLogin = payment.PaymentMethod.DynamicProperty<string>().ApiLogin;
				bool instantAcquire = payment.PaymentMethod.DynamicProperty<bool>().InstantAcquire;

				var hashComputer = new AuthorizedotnetMd5Computer();
				if (hashComputer.IsMatch(md5Hash, apiLogin, transact, amountParameter, md5KeyParameter))
					paymentStatus = instantAcquire ? PaymentStatusCode.Acquired : PaymentStatusCode.Authorized;

				payment.PaymentStatus = PaymentStatus.Get((int) paymentStatus);
				payment.TransactionId = transact;
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}

			// Configuration values
			string declineUrl = payment.PaymentMethod.DynamicProperty<string>().DeclineUrl;
			string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;

			HttpContext.Current.Response.Write(paymentStatus == PaymentStatusCode.Declined
									? DownloadPageContent(new Uri(_absoluteUrlService.GetAbsoluteUrl(declineUrl))
											.AddQueryStringParameter("x_response_reason_code", responseReasonCodeParameter)
											.AddQueryStringParameter("x_response_reason_text", responseReasonTextParameter), payment)

									: DownloadPageContent(new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)), payment));
        }

    	/// <summary>
    	/// Downloads the content of the accept or decline Url.
    	/// </summary>
    	/// <param name="uri">The URI.</param>
    	/// <param name="payment">The payment.</param>
    	/// <returns></returns>
    	/// <remarks>This method converts the HTML page of the accept or decline url into a string. 
    	/// Instead of rederecting Authorize.NET in the <see cref="ProcessCallback"/>, we write the HTML page directly to them
    	/// in their request. This method Returns the string to write to Authorize.NET.
    	/// </remarks>
    	private string DownloadPageContent(Uri uri, Payment payment)
		{
			var client = new WebClient();
			var requestedHtml = client.DownloadData(uri.AddOrderGuidParameter(payment.PurchaseOrder));
			var encoding = new UTF8Encoding();
			return encoding.GetString(requestedHtml);
		}

		/// <summary>
		/// Cancels the payment from Authorize.NET.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		/// <remarks>Method uses Authorize.NET SDK to cancel the payment.</remarks>
        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
			// Configuration values
			string apiLogin = payment.PaymentMethod.DynamicProperty<string>().ApiLogin;
			string transactionKey = payment.PaymentMethod.DynamicProperty<string>().TransactionKey;
			bool testMode = payment.PaymentMethod.DynamicProperty<bool>().TestMode;

            var gateway = new Gateway(apiLogin, transactionKey, testMode);
            IGatewayResponse gatewayResponse = gateway.Send(new VoidRequest(payment.TransactionId));
			status = gatewayResponse.Message;
            return gatewayResponse.Approved;
        }

		/// <summary>
		/// Acquires the payment from Authorize.NET.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		/// <remarks>Method uses Authorize.NET SDK to Acquire the payment.</remarks>
        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
			// Configuration values
			string apiLogin = payment.PaymentMethod.DynamicProperty<string>().ApiLogin;
			string transactionKey = payment.PaymentMethod.DynamicProperty<string>().TransactionKey;
			bool testMode = payment.PaymentMethod.DynamicProperty<bool>().TestMode;
			
			var gateway = new Gateway(apiLogin, transactionKey, testMode);
            IGatewayResponse gatewayResponse = gateway.Send(new PriorAuthCaptureRequest(payment.Amount, payment.TransactionId));
            status = gatewayResponse.Message;
            return gatewayResponse.Approved;
        }

		/// <summary>
		/// This method is not supported as Authorize.NET not allows refunds.
		/// </summary>
		/// <param name="payment">The payment.</param>
		/// <param name="status">The status.</param>
		/// <returns></returns>
		/// <remarks></remarks>
        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
            status = "Refund payment is not available due to security reasons. Manual refunding is available at https://secure.authorize.net/gateway/transact.dll";
            return false;
        }
    }
}

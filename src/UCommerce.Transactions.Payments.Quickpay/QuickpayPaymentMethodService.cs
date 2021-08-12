using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Environment;
using Ucommerce.Transactions.Payments.Common;

namespace Ucommerce.Transactions.Payments.Quickpay
{
    /// <summary>
    /// Quickpay integration via hosted payment form.
    /// </summary>
    public class QuickpayPaymentMethodService : ExternalPaymentMethodService
    {
	    private readonly IWebRuntimeInspector _webRuntimeInspector;
	    private QuickpayMd5Computer QuickpayMd5Computer { get; set; }
		private AbstractPageBuilder PageBuilder { get; set; }

	    private const string API_ENDPOINT_URL = "https://secure.quickpay.dk/api";

	    /// <summary>
        /// Initializes a new instance of the <see cref="QuickpayPaymentMethodService"/> class.
        /// </summary>
        public QuickpayPaymentMethodService(QuickpayPageBuilder pageBuilder, QuickpayMd5Computer md5Computer, IWebRuntimeInspector webRuntimeInspector)
		{
		    _webRuntimeInspector = webRuntimeInspector;
		    QuickpayMd5Computer = md5Computer;
			PageBuilder = pageBuilder;
		}

        protected virtual string PROTOCOL => "6";

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
	        Guard.Against.PaymentNotPendingAuthorization(payment);
			Guard.Against.MissingHttpContext(_webRuntimeInspector);
			Guard.Against.MissingRequestParameter("transactionId");

	        bool instantAcquire = payment.PaymentMethod.DynamicProperty<bool>().InstantAcquire;
			int transactionId = GetTransactionIdFromRequestParameters(HttpContext.Current.Request["transactionId"]);
			
			bool callbackValid = ValidateCallback(payment.PaymentMethod);

	        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Declined);
			payment.TransactionId = transactionId.ToString();
 
			if (callbackValid)
			{
				payment.PaymentStatus = instantAcquire
					                        ? PaymentStatus.Get((int)PaymentStatusCode.Acquired)
					                        : PaymentStatus.Get((int)PaymentStatusCode.Authorized);
				
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}
        }

	    protected int GetTransactionIdFromRequestParameters(string input)
	    {
		    int transactionId;
			if (!int.TryParse(input, out transactionId))
				throw new FormatException(@"transaction must be a valid int32.");

		    return transactionId;
	    }

	    protected virtual bool ValidateCallback(PaymentMethod paymentMethod)
	    {
			string md5Secret = paymentMethod.DynamicProperty<string>().Md5secret;
		    string[] requestFieldNames =
			    {
				    "msgtype",
				    "ordernumber",
				    "amount",
				    "currency",
				    "time",
				    "state",
				    "qpstat",
				    "qpstatmsg",
				    "chstat",
				    "chstatmsg",
				    "merchant",
				    "merchantemail",
				    "transaction",
				    "cardtype",
				    "cardnumber",
				    "cardexpire",
				    "splitpayment",
				    "fraudprobability",
				    "fraudremarks",
				    "fraudreport",
				    "fee"
			    };

	        var sb = new StringBuilder();
		    foreach (string field in requestFieldNames)
			    sb.Append(HttpContext.Current.Request[field]);

		    string md5Response = QuickpayMd5Computer.GetMd5KeyFromResponseValueString(sb.ToString(),md5Secret);
		    string md5Check = HttpContext.Current.Request["md5check"];
		    string quickPayStatus = HttpContext.Current.Request["qpstat"];

		    return quickPayStatus.Equals("000") && md5Response.Equals(md5Check);
	    }

        /// <summary>
        /// Determines if the API operation was successful
        /// </summary>
        /// <param name="message">The XML response string.</param>
        /// <param name="paymentMethod">The payment method.</param>
        /// <returns>The call status</returns>
        private bool ValidateApiCall(string message, PaymentMethod paymentMethod)
	    {
			string md5Secret = paymentMethod.DynamicProperty<string>().Md5secret;

            string md5ResponseString = "";
            string md5Check = "";

            var responseElement = XDocument.Parse(message).Element("response");

		    if (responseElement == null) return false;

			// Concat all elements for MD5 check to
			// validate the returned response.
			// Make sure to exclude to sent MD5 value.
            md5ResponseString = responseElement.Descendants()
                .Where(x => x.Name.ToString() != "md5check")
                .Select(x => x.Value)
                .Aggregate((a, b) => a + b);

            md5Check = responseElement.Element("md5check").Value;

	        string status = responseElement.Element("qpstat").Value;

	        string md5CheckResponse = QuickpayMd5Computer.GetMd5KeyFromResponseValueString(md5ResponseString,md5Secret);

	        return status.Equals("000") 
				&& !String.IsNullOrEmpty(md5Check) 
				&& !String.IsNullOrEmpty(md5CheckResponse) 
				&& md5Check.Equals(md5CheckResponse);
	    }

        /// <summary>
        /// Gets the Quickpay status message.
        /// </summary>
        /// <param name="message">The quickpay response message.</param>
        /// <returns>Call status message string.</returns>
        protected string GetCallStatusMessage(string message)
        {
            var el = XDocument.Parse(message).Element("response");
            string qpstat = el.Element("qpstat").Value;
            string chstat = el.Element("chstat").Value;
            string chstatmsg = el.Element("chstatmsg").Value;

            string status = "";
            switch (qpstat)
            {
                case "000":
                    status = "Approved.";
                    break;
                case "001":
                    status = "Rejected by acquirer: " + chstat + ": " + chstatmsg;
                    break;
                case "002":
                    status = "Communication Error.";
                    break;
                case "003":
                    status = "Card expired.";
                    break;
                case "004":
                    status = "Transition is not allowed for transaction current state.";
                    break;
                case "005":
                    status = "Authorization is expired.";
                    break;
                case "006":
                    status = "Error reported by acquirer.";
                    break;
                case "007":
                    status = "Error reported by QuickPay.";
                    break;
                case "008":
                    status = "Error in request data.";
                    break;
                case "009":
                    status = "Payment aborted by shopper";
                    break;
            }

            return status;
        }

        private Dictionary<string, string> GetDefaultPostValues(PaymentMethod paymentMethod)
        {
			string merchant = paymentMethod.DynamicProperty<string>().Merchant.ToString();
			string apiKey = paymentMethod.DynamicProperty<string>().ApiKey.ToString();

            var postValues = new Dictionary<string, string>();
            postValues.Add("protocol", PROTOCOL);
			postValues.Add("merchant", merchant);
			postValues.Add("apikey", apiKey);

            return postValues;
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
			string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();
			string md5Secret = payment.PaymentMethod.DynamicProperty<string>().Md5secret.ToString();

            var postValues = GetDefaultPostValues(payment.PaymentMethod);
            postValues.Add("msgtype", "cancel");
            postValues.Add("transaction", payment.TransactionId);
			postValues.Add("md5check", QuickpayMd5Computer.GetCancelPreMd5Key(PROTOCOL, payment.TransactionId, merchant, apiKey, md5Secret));

            var httpPost = new HttpPost(API_ENDPOINT_URL, postValues);

            string postResponse = httpPost.GetString();
            bool callStatus = ValidateApiCall(postResponse,payment.PaymentMethod);

            if (callStatus)
                status = PaymentMessages.CancelSuccess + " >> " + postResponse;
            else
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.CancelFailed, GetCallStatusMessage(postResponse), postResponse);

            return callStatus;
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
			string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();
            string md5Secret = payment.PaymentMethod.DynamicProperty<string>().Md5secret.ToString();

            var postValues = GetDefaultPostValues(payment.PaymentMethod);
            postValues.Add("msgtype", "capture");
            postValues.Add("amount", payment.Amount.ToCents().ToString());
            postValues.Add("transaction", payment.TransactionId);
            postValues.Add("md5check", QuickpayMd5Computer.GetAcquirePreMd5Key(PROTOCOL, payment.Amount.ToCents().ToString(), payment.TransactionId,merchant,apiKey,md5Secret));

            var httpPost = new HttpPost(API_ENDPOINT_URL, postValues);

            string postResponse = httpPost.GetString();
            bool callStatus = ValidateApiCall(postResponse,payment.PaymentMethod);

            if (callStatus)
                status = PaymentMessages.AcquireSuccess + " >> " + postResponse;
            else
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.AcquireFailed, GetCallStatusMessage(postResponse), postResponse);

            return callStatus;
        }

        /// <summary>
        /// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="status">The status.</param>
        /// <returns></returns>
        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
			string merchant = payment.PaymentMethod.DynamicProperty<string>().Merchant.ToString();
			string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();
			string md5Secret = payment.PaymentMethod.DynamicProperty<string>().Md5secret.ToString();

            var postValues = GetDefaultPostValues(payment.PaymentMethod);
            postValues.Add("msgtype", "refund");
            postValues.Add("amount", payment.Amount.ToCents().ToString());
            postValues.Add("transaction", payment.TransactionId);
            postValues.Add("md5check", QuickpayMd5Computer.GetRefundPreMd5Key(PROTOCOL, payment.Amount.ToCents().ToString(), payment.TransactionId,merchant,apiKey,md5Secret));

            var httpPost = new HttpPost(API_ENDPOINT_URL, postValues);

            string postResponse = httpPost.GetString();
            bool callStatus = ValidateApiCall(postResponse,payment.PaymentMethod);

            if (callStatus)
                status = PaymentMessages.RefundSuccess + " >> " + postResponse;
            else
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.RefundFailed, GetCallStatusMessage(postResponse), postResponse);

            return callStatus;
        }
    }
}

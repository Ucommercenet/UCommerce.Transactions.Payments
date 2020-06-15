using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Ideal
{
    public class IdealPaymentMethodService : ExternalPaymentMethodService
    {
        private IdealPageBuilder IdealPageBuilder { get; set; }

        public IdealPaymentMethodService(IdealPageBuilder idealPageBuilder)
        {
            IdealPageBuilder = idealPageBuilder;
        }

        public override string RenderPage(PaymentRequest paymentRequest)
        {
            return IdealPageBuilder.Build(paymentRequest);
        }

        /// <summary>
        /// Validates that the currency matches the provider
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <returns></returns>
        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {
            string currencyIsoCode = paymentRequest.Amount.CurrencyIsoCode;
            if (currencyIsoCode.ToLower() != "eur")
                throw new InvalidOperationException(
                    string.Format(
                        "iDEAL payments doesn't support {0} as currency, EUR is the only currency supported. To use iDEAL payments please change PaymentRequest to use EUR.",
                        currencyIsoCode));

            return base.RequestPayment(paymentRequest);
        }

        /// <summary>
        /// Extracts the transaction result from the body of the HTTP request
        /// </summary>
        /// <param name="payment"></param>
        public override void ProcessCallback(Payment payment)
        {
            if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
                return;

            var response = payment["response"];
            if (response == null)
                throw new NullReferenceException(
                    @"response was not found in payment[""response""], please make sure to insert the response string in the Extract method");

            XDocument xmlBody = XDocument.Parse(response);

            XNamespace xNs = "http://www.idealdesk.com/Message";

            var xmlTransactionStatus = xmlBody.Descendants(xNs + "status").FirstOrDefault();
            if (xmlTransactionStatus == null)
                throw new ArgumentException("Status was not found in XML returned to PaymentProcessor.axd. Expected response with XML element 'status' included.");


        	bool requestSuccessful = xmlTransactionStatus.Value.ToLower() == "success";
        	var paymentStatus = requestSuccessful
                                    ? PaymentStatusCode.Acquired
                                    : PaymentStatusCode.Declined;

            var xmlTransactionId = xmlBody.Descendants(xNs + "transactionID").FirstOrDefault();
            if (xmlTransactionId == null)
                throw new ArgumentException("Transaction ID was not found in xml returned with PaymentProcessor.axd. Expected response with XML element 'transactionID'.");

            string transactionId = xmlTransactionId.Value;

            payment.PaymentStatus = PaymentStatus.Get((int)paymentStatus);
            payment.TransactionId = transactionId;
			
			if (requestSuccessful)
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
        }

        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
            throw new NotSupportedException(
                "Explicit acquire is not supported by iDEAL (ING). Acquire happens immediately after authorization.");
        }

        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
            throw new NotSupportedException(
                "Cancel is not supported by iDEAL (ING). If you want to cancel a transaction use the dashboard provided by the bank.");
        }

        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
            throw new NotSupportedException(
                "Refund is not supported by iDEAL (ING). If you want to cancel a transaction use the dashboard provided by the bank.");
        }

        /// <summary>
        /// Extracts <see cref="Payment"/> from the body of the HTTP request performed during callback from iDEAL.
        /// </summary>
        /// <remarks>
        /// iDEAL is special because it�s URL is static thus only contains the paymentMethodId.
        /// To find the payment in question the method will extract the body of the HTTP request and find the purchaseID in the XML payload.
        /// Payment will contain the full XML response from iDEAL ING in a custom property called "response".
        /// </remarks>
        /// <param name="request">HttpRequest to extract</param>
        /// <returns></returns>
        public override Payment Extract(System.Web.HttpRequest request)
        {
            Stream inputStream = request.InputStream;
            int length = Convert.ToInt32(inputStream.Length);

            byte[] byteArr = new byte[length];
            inputStream.Read(byteArr, 0, length);

            var stringBuilder = new StringBuilder();
            foreach (var b in byteArr)
                stringBuilder.Append(char.ConvertFromUtf32(b));

            XDocument xmlBody = XDocument.Parse(stringBuilder.ToString());

            XNamespace xNs = "http://www.idealdesk.com/Message";

            var purchaseId = xmlBody.Descendants(xNs + "purchaseID").FirstOrDefault();
            if (purchaseId == null)
                throw new NullReferenceException("purchaseID was not found in XML returned to extractor.");

            var payment = Payment.All().SingleOrDefault(x => x.ReferenceId == purchaseId.Value);
            if (payment == null)
                throw new NullReferenceException(string.Format("Could not find a payment with ReferenceId: '{0}'.",
                                                               purchaseId.Value));

                payment["response"] = stringBuilder.ToString();

            return payment;
        }
    }
}

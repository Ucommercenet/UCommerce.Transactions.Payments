using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Environment;
using Ucommerce.Transactions.Payments.Common;

namespace Ucommerce.Transactions.Payments.Quickpay
{
    public class QuickpayV10PaymentMethodService : QuickpayPaymentMethodService
    {
        private readonly IWebRuntimeInspector _webRuntimeInspector;

        public QuickpayV10PaymentMethodService(QuickpayPageBuilder pageBuilder, QuickpayMd5Computer md5Computer, IWebRuntimeInspector webRuntimeInspector) : base(pageBuilder, md5Computer, webRuntimeInspector)
        {
            _webRuntimeInspector = webRuntimeInspector;
        }

        protected override string PROTOCOL => "v10";


        /// <summary>
        /// Processed the callback received from the payment provider.
        /// </summary>
        /// <param name="payment">The payment.</param>
        public override void ProcessCallback(Payment payment)
        {
            Guard.Against.PaymentNotPendingAuthorization(payment);
            Guard.Against.MissingHttpContext(_webRuntimeInspector);
            var dto = ReadCallbackBody(HttpContext.Current);

            bool instantAcquire = payment.PaymentMethod.DynamicProperty<bool>().InstantAcquire;
            int transactionId = base.GetTransactionIdFromRequestParameters(dto.Id);

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

        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
            string resourceUrl =
                $"payments/{(object)payment.TransactionId}/refund?amount={(object)payment.Amount.ToCents()}";
            const string method = "POST";

            string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey;

            var request = CreateWebRequest(apiKey, resourceUrl, method);
            using var response = (HttpWebResponse)request.GetResponse();
            var result = ValidateCaptureStatus(response);
            if (result)
                status = PaymentMessages.RefundSuccess;
            else
            {
                var responseMessage = ReadResponseMessage(response) ?? "Failed to Cancel the Payment";
                status = $"{PaymentMessages.RefundFailed} >> {GetCallStatusMessage(responseMessage)}";
            }

            return result;
        }

        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
            var resourceUrl = $"payments/{payment.TransactionId}/cancel";
            const string method = "POST";

            string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey;

            var request = CreateWebRequest(apiKey, resourceUrl, method);
            using var response = (HttpWebResponse)request.GetResponse();
            var result = ValidateCaptureStatus(response);
            if (result)
                status = PaymentMessages.CancelSuccess;
            else
            {
                var responseMessage = ReadResponseMessage(response) ?? "Failed to Cancel the Payment";
                status = $"{PaymentMessages.CancelFailed} >> {GetCallStatusMessage(responseMessage)}";
            }
            
            return result;
        }

        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
            string resourceUrl =
                $"payments/{(object) payment.TransactionId}/capture?amount={(object) payment.Amount.ToCents()}";
            const string method = "POST";
            
            string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey;

            var request = CreateWebRequest(apiKey, resourceUrl, method);
            using var response = (HttpWebResponse)request.GetResponse();
            var result = ValidateCaptureStatus(response);
            if (result)
                status = PaymentMessages.AcquireSuccess;
            else
            {
                var responseMessage = ReadResponseMessage(response) ?? "Failed to Acquire the Payment";
                status = $"{PaymentMessages.AcquireFailed} >> {GetCallStatusMessage(responseMessage)}";
            }

            return result;
        }

        private string ReadResponseMessage(HttpWebResponse response)
        {
            using var reader = new System.IO.StreamReader(response.GetResponseStream());
            return reader.ReadToEnd();
        }

        private bool ValidateCaptureStatus(HttpWebResponse response)
        {
            var responseStatusCode = response.StatusCode;

            switch (responseStatusCode)
            {
                case HttpStatusCode.Accepted:
                    return true;
                default:
                    return false;
            }
        }

        protected override bool ValidateCallback(PaymentMethod paymentMethod)
        {
            string checkSum = HttpContext.Current.Request.Headers["QuickPay-Checksum-Sha256"];

            var bytes = new byte[HttpContext.Current.Request.InputStream.Length];
            HttpContext.Current.Request.InputStream.Read(bytes, 0, bytes.Length);
            HttpContext.Current.Request.InputStream.Position = 0;
            string content = Encoding.UTF8.GetString(bytes);

            string compute = Sign(content, paymentMethod.DynamicProperty<string>().PrivateAccountKey);

            return checkSum.Equals(compute);
        }

        private string Sign(string content, string apiKey)
        {
            var e = Encoding.UTF8;

            var hmac = new HMACSHA256(e.GetBytes(apiKey));
            byte[] b = hmac.ComputeHash(e.GetBytes(content));

            var s = new StringBuilder();
            for (int i = 0; i < b.Length; i++)
            {
                s.Append(b[i].ToString("x2"));
            }

            return s.ToString();
        }

        private QuickpayApiResponseDto ReadCallbackBody(
            HttpContext currentHttpContext)
        {
            currentHttpContext.Request.InputStream.Position = 0L;
            StreamReader streamReader = new StreamReader(currentHttpContext.Request.InputStream);
            streamReader.BaseStream.Seek(0L, SeekOrigin.Begin);
            string end = streamReader.ReadToEnd();
            currentHttpContext.Request.InputStream.Position = 0L;
            return (QuickpayApiResponseDto)JsonConvert.DeserializeObject<QuickpayApiResponseDto>(end);
        }

        private HttpWebRequest CreateWebRequest(
            string apiKey,
            string resource,
            string method)
        {
            string requestUriString = "https://api.quickpay.net/" + resource;
            string base64String = Convert.ToBase64String(Encoding.Default.GetBytes(":" + apiKey));
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUriString);
            httpWebRequest.Headers["Authorization"] = "Basic " + base64String;
            httpWebRequest.Method = method;
            httpWebRequest.Headers.Add("accept-version", "v10");
            return httpWebRequest;
        }
    }
}

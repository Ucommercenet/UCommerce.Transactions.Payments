using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ucommerce.EntitiesV2;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Transactions.Payments.QuickpayLink.Models;

namespace Ucommerce.Transactions.Payments.QuickpayLink
{
    internal class QuickpayServiceClient
    {
        private const string ApiVersion = "v10";
        private const string PaymentCanceled = "cancel";
        private const string PaymentRefunded = "refund";
        private const string PaymentCaptured = "capture";
        private const string BasePath = "https://api.quickpay.net/";
        private readonly string _apiKey;

        public QuickpayServiceClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>
        /// Creates a new payment.
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="currency"></param>
        /// <returns></returns>
        /// <exception cref="HttpRequestException"></exception>
        public PaymentModel CreatePayment(string orderId, string currency)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("currency", currency),
                new KeyValuePair<string, string>("order_id", orderId)
            });

            var response = Task.Run(async () => await CreateWebRequest("payments", Method.POST, formContent))
                               .ConfigureAwait(false)
                               .GetAwaiter()
                               .GetResult();

            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw new HttpRequestException("Payment not created.");
            }

            try
            {
                return DeseralizeContent<PaymentModel>(response.Content);
            } 
            catch (Exception) 
            {
                return null;
            }
        }

        /// <summary>
        /// Creates or updates a payment link.
        /// </summary>
        /// <param name="requestParams"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        public string CreatePaymentLink(CreatePaymentLinkParams requestParams)
        {
            if (requestParams.Id == 0)
            {
                throw new ArgumentException("Payment ID not set.");
            }

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("amount", requestParams.Amount.ToString()),
                new KeyValuePair<string, string>("continue_url", requestParams.AcceptUrl),
                new KeyValuePair<string, string>("cancel_url", requestParams.CancelUrl),
                new KeyValuePair<string, string>("payment_methods", requestParams.PaymentMethods)
            });

            var response = Task.Run(async () => await CreateWebRequest($"payments/{requestParams.Id}/link", Method.PUT, formContent))
                   .ConfigureAwait(false)
                   .GetAwaiter()
                   .GetResult();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Invalid parameters. Payment link not created.");
            }

            // URL is a json object of {url: <url>}
            var url = Task.Run(async () => await response.Content.ReadAsStringAsync())
                       .ConfigureAwait(false)
                       .GetAwaiter()
                       .GetResult();

            return JsonConvert.DeserializeObject<CreatePaymentLinkResponse>(url).Url;
        }

        /// <summary>
        /// Returns the payment.
        /// </summary>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        /// <exception cref="HttpRequestException"></exception>
        public PaymentModel GetPayment(int paymentId)
        {
            var response = Task.Run(async () => await CreateWebRequest($"payments/{paymentId}", Method.GET))
                               .ConfigureAwait(false)
                               .GetAwaiter()
                               .GetResult();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Payment not found.");
            }

            return DeseralizeContent<PaymentModel>(response.Content);
        }

        /// <summary>
        /// Captures the payment.
        /// </summary>
        /// <param name="payment"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool CapturePayment(Payment payment, out string state)
        {
            var paymentDto = GetPayment(int.Parse(payment.TransactionId));
            if (paymentDto.State == PaymentStatus.Processed)
            {
                state = paymentDto.State;
                return false;
            }

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("amount", Convert.ToInt32(payment.Amount.ToCents()).ToString()),
            });

            var response = Task.Run(async () => await CreateWebRequest($"payments/{payment.TransactionId}/capture", Method.POST, formContent))
                   .ConfigureAwait(false)
                   .GetAwaiter()
                   .GetResult();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                state = "Bad request";
                return false;
            }

            paymentDto = DeseralizeContent<PaymentModel>(response.Content);
            state = paymentDto.State;
            return paymentDto.Operations.Any(x => x.Type == PaymentCaptured);
        }

        /// <summary>
        /// Cancels the payment.
        /// </summary>
        /// <param name="paymentId"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool CancelPayment(int paymentId, out string state)
        {
            var response = Task.Run(async () => await CreateWebRequest($"payments/{paymentId}/cancel", Method.POST))
                               .ConfigureAwait(false)
                               .GetAwaiter()
                               .GetResult();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                state = "Bad request";
                return false;
            }

            var paymentDto = DeseralizeContent<PaymentModel>(response.Content);
            state = paymentDto.State;
            return paymentDto.Operations.Any(x => x.Type == PaymentCanceled);
        }

        /// <summary>
        /// Refunds the payment.
        /// </summary>
        /// <param name="payment"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool RefundPayment(Payment payment, out string state)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("amount", Convert.ToInt32(payment.Amount.ToCents()).ToString()),
            });

            var response = Task.Run(async () => await CreateWebRequest($"payments/{payment.TransactionId}/refund", Method.POST, formContent))
                   .ConfigureAwait(false)
                   .GetAwaiter()
                   .GetResult();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                state = "Bad request";
                return false;
            }

            var paymentDto = DeseralizeContent<PaymentModel>(response.Content);
            state = paymentDto.State;
            return paymentDto.Operations.Any(x => x.Type == PaymentRefunded);
        }

        #region Private methods
        private async Task<HttpResponseMessage> CreateWebRequest(string url, Method method, HttpContent content = null)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.Default.GetBytes($":{_apiKey}"))}");
            httpClient.DefaultRequestHeaders.Add("accept-version", ApiVersion);

            var uri = $"{BasePath}{url}";
            HttpResponseMessage reponse;
            switch (method)
            {
                case Method.GET:
                    reponse = await httpClient.GetAsync(uri);
                    break;

                case Method.POST:
                    reponse = await httpClient.PostAsync(uri, content);
                break;

                case Method.PUT:
                    reponse = await httpClient.PutAsync(uri, content);
                    break;

                default:
                    throw new ArgumentException($"Http Method not implemented: {nameof(method)}.");
            }

            return reponse;
        }

        private T DeseralizeContent<T>(HttpContent content)
        {
            var contentStr = Task.Run(async () => await content.ReadAsStringAsync())
                               .ConfigureAwait(false)
                               .GetAwaiter()
                               .GetResult();

            return JsonConvert.DeserializeObject<T>(contentStr);
        }
        #endregion
    }

    /// <summary>
    /// Represents the set of http methods the client supports.
    /// </summary>
    internal enum Method
    {
        GET,
        POST,
        PUT
    }
}

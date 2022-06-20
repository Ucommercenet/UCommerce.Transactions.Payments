using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Environment;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Transactions.Payments;
using Ucommerce.Web;
using Ucommerce.Infrastructure.Logging;

namespace Ucommerce.Transactions.Payments.Freepay
{
    public class OperationResultModel
    {
        public bool IsSuccess { get; set; }
        public int GatewayStatusCode { get; set; }
        public string GatewayStatusMessage { get; set; }
    }

    public class TransactionModel
    {
        public int AuthorizationID { get; set; }
        public int MerchantNumber { get; set; }
        public Guid AuthorizationIdentifier { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateAuthorized { get; set; }
        public DateTime? DateCaptured { get; set; }
        public string Currency { get; set; }
        public string OrderID { get; set; }
        public int CardType { get; set; }
        public int AuthorizationAmount { get; set; }
        public bool IsCaptured { get; set; }
        public int CaptureAmount { get; set; }
        public int CaptureErrorCode { get; set; }
        public bool IsSubscription { get; set; }
        public DateTime DateSubscriptionExpires { get; set; }
        public DateTime? DateCredited { get; set; }
        public string MaskedPan { get; set; }
        public bool Used3dSecure { get; set; }
        public int? Acquirer { get; set; }
        public int Status { get; set; }
        public Guid? PaymentIdentifier { get; set; }
        public string Wallet { get; set; }
        public int WalletProvider { get; set; }
        public string CardExpiryDate { get; set; }
    }

    /// <summary>
    /// Freepay integration via hosted payment form.
    /// </summary>
    public class FreepayPaymentMethodService : ExternalPaymentMethodService
    {
        private readonly IAbsoluteUrlService _absoluteUrlService;
        private readonly ICallbackUrl _callbackUrl;
        private readonly IWebRuntimeInspector _webRuntimeInspector;
        private readonly ILoggingService _loggingService;
        private AbstractPageBuilder PageBuilder { get; set; }

	    private const string API_ENDPOINT_URL = "https://mw.freepay.dk/api/v2";

	    /// <summary>
        /// Initializes a new instance of the <see cref="FreepayPaymentMethodService"/> class.
        /// </summary>
        public FreepayPaymentMethodService(IWebRuntimeInspector webRuntimeInspector, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl, ILoggingService loggingService)
		{
		    _webRuntimeInspector = webRuntimeInspector;
            _absoluteUrlService = absoluteUrlService;
            _callbackUrl = callbackUrl;
            _loggingService = loggingService;
        }

        /// <summary>
        /// Renders the forms to be submitted to the payment provider.
        /// </summary>
        /// <param name="paymentRequest">The payment request.</param>
        /// <returns>A string containing the html form.</returns>
        public override string RenderPage(PaymentRequest paymentRequest)
        {
            throw new NotSupportedException("Freepay does not need a local form. Use RequestPayment instead.");
        }

        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {
            if (paymentRequest?.Payment == null)
            {
                paymentRequest.Payment = CreatePayment(paymentRequest);
            }

            string url = GeneratePaymentLink(paymentRequest);
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("Could not redirect to Freepay payment page.");
            }

            HttpContext.Current.Response.Redirect(url);

            return paymentRequest.Payment;
        }

        /// <summary>
        /// Processed the callback received from the payment provider.
        /// </summary>
        /// <param name="payment">The payment.</param>
        public override void ProcessCallback(Payment payment)
        {
	        Guard.Against.PaymentNotPendingAuthorization(payment);
			Guard.Against.MissingHttpContext(_webRuntimeInspector);
			Guard.Against.MissingRequestParameter("authorizationIdentifier");

			Guid authorizationId = GetTransactionIdFromRequestParameters(HttpContext.Current.Request.Form["authorizationIdentifier"]);

            bool callbackValid = ValidateCallback(payment, authorizationId.ToString());

	        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Declined);
			payment.TransactionId = authorizationId.ToString();

            if (callbackValid)
			{
				payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
				
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}
        }

	    private Guid GetTransactionIdFromRequestParameters(string input)
	    {
            if (!Guid.TryParse(input, out Guid id))
            {
                throw new FormatException(@"Identifier must be a valid Guid.");
            }

		    return id;
	    }

	    private bool ValidateCallback(Payment payment, string authorizationIdentifier)
	    {
            try
            {
                var result = MakeRequest(null, HttpMethod.Get, API_ENDPOINT_URL + $"/authorization/{authorizationIdentifier}", payment.PaymentMethod.DynamicProperty<Guid>().ApiKey.ToString(), out HttpStatusCode responseCode);

                if (!string.IsNullOrEmpty(result))
                {
                    TransactionModel transaction = JsonConvert.DeserializeObject<TransactionModel>(result);
                    if (transaction.AuthorizationAmount == payment.Amount.ToCents() && transaction.Currency == payment.PurchaseOrder.BillingCurrency.ISOCode && transaction.OrderID == payment.PurchaseOrder.Id.ToString() && !transaction.IsCaptured)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error<FreepayPaymentMethodService>(ex, "Error validating callback!");
            }

            return false;
	    }

        /// <summary>
        /// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="status">The status.</param>
        /// <returns></returns>
        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
			string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();

            try
            {
                var result = MakeRequest(null, HttpMethod.Delete, API_ENDPOINT_URL + $"/authorization/{payment.TransactionId}", apiKey, out HttpStatusCode responseCode);

                if (responseCode == HttpStatusCode.OK || responseCode == HttpStatusCode.NoContent)
                {
                    status = PaymentMessages.CancelSuccess;
                }
                else
                {
                    status = String.Format("{0} >> {1}", PaymentMessages.CancelFailed, result);
                }
            }
            catch (Exception ex)
            {
                status = String.Format("{0} >> {1}", PaymentMessages.CancelFailed, ex.Message);
            }

            return true;
        }

        /// <summary>
        /// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="status">The status.</param>
        /// <returns></returns>
        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
			string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();

            try
            {
                var result = MakeRequest(JsonConvert.SerializeObject(new { Amount = payment.Amount.ToCents() }), HttpMethod.Post, API_ENDPOINT_URL + $"/authorization/{payment.TransactionId}/capture", apiKey, out HttpStatusCode responseCode);

                if (responseCode == HttpStatusCode.OK || responseCode == HttpStatusCode.NoContent)
                {
                    OperationResultModel resultData = JsonConvert.DeserializeObject<OperationResultModel>(result);

                    if (resultData.IsSuccess)
                    {
                        status = PaymentMessages.AcquireSuccess;
                    }
                    else
                    {
                        status = string.Format("{0} >> {1}", PaymentMessages.AcquireFailed, "Capture error code: " + resultData.GatewayStatusCode);
                    }
                }
                else
                {
                    status = string.Format("{0} >> {1}", PaymentMessages.AcquireFailed, result);
                }
            }
            catch (Exception ex)
            {
                status = string.Format("{0} >> {1}", PaymentMessages.AcquireFailed, ex.Message);
            }

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
			string apiKey = payment.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();

            try
            {
                var result = MakeRequest(JsonConvert.SerializeObject(new { Amount = payment.Amount.ToCents() }), HttpMethod.Post, API_ENDPOINT_URL + $"/authorization/{payment.TransactionId}/credit", apiKey, out HttpStatusCode responseCode);

                if (responseCode == HttpStatusCode.OK || responseCode == HttpStatusCode.NoContent)
                {
                    OperationResultModel resultData = JsonConvert.DeserializeObject<OperationResultModel>(result);
                    
                    if (resultData.IsSuccess)
                    {
                        status = PaymentMessages.RefundSuccess;
                    }
                    else
                    {
                        status = string.Format("{0} >> {1}", PaymentMessages.AcquireFailed, resultData.GatewayStatusMessage);
                    }
                }
                else
                {
                    status = string.Format("{0} >> {1}", PaymentMessages.RefundFailed, result);
                }
            }
            catch (Exception ex)
            {
                status = string.Format("{0} >> {1}", PaymentMessages.RefundFailed, ex.Message);
            }

            return true;
        }

        private string MakeRequest(string body, HttpMethod method, string path, string apiKey, out HttpStatusCode responseCode)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(path);
            request.Method = method.ToString().ToUpper();
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", apiKey);
            if (!string.IsNullOrEmpty(body))
            {
                request.ContentLength = (long)body.Length;
                StreamWriter streamWriter = new StreamWriter(request.GetRequestStream(), Encoding.ASCII);
                streamWriter.Write(RuntimeHelpers.GetObjectValue(body));
                streamWriter.Close();
            }

            var response = (HttpWebResponse)request.GetResponse();
            var content = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8")).ReadToEnd();

            responseCode = response.StatusCode;

            return content;
        }

        private string GeneratePaymentLink(PaymentRequest paymentRequest)
        {
            string apiKey = paymentRequest.PaymentMethod.DynamicProperty<string>().ApiKey.ToString();
            string acceptUrlConfig = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;
            string declineUrlConfig = paymentRequest.PaymentMethod.DynamicProperty<string>().CancelUrl;
            bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;

            var twoLetterLanguageCode = GetFourLetterLanguageCode(paymentRequest);
            var amount = paymentRequest.Payment.Amount.ToCents().ToString();
            var acceptUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrlConfig)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
            var declineUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(declineUrlConfig)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
            var callbackUrl = _callbackUrl.GetCallbackUrl("(auto)", paymentRequest.Payment);

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", apiKey);

            var values = new Dictionary<string, object>
            {
                { "Amount", amount },
                { "Currency", paymentRequest.Amount.CurrencyIsoCode },
                { "OrderNumber", paymentRequest.PurchaseOrder.Id },
                { "CustomerAcceptUrl", acceptUrl },
                { "CustomerDeclineUrl", declineUrl },
                { "ServerCallbackUrl", callbackUrl },
                { "BillingAddress", GetAddressJson(paymentRequest.PurchaseOrder, true) },
                { "ShippingAddress", GetAddressJson(paymentRequest.PurchaseOrder) },
                { "SaveCard", false },
                { "EnforceLanguage", twoLetterLanguageCode },
                { "Client", new Dictionary<string, object> {
                    { "CMS", new Dictionary<string, object> { { "Name", "Umraco" }, { "Version", "8" } } },
                    { "Shop", new Dictionary<string, object> { { "Name", "uCommerce" }, { "Version", "9.4.2" } } },
                    { "Plugin", new Dictionary<string, object> { { "Name", "Freepay" }, { "Version", "1.1" } } },
                    { "API", new Dictionary<string, object> { { "Name", "Freepay" }, { "Version", "2.0" } } },
                } },
            };

            if (testMode)
            {
                values.Add("Options", new Dictionary<string, object> { { "TestMode", true } });
            }

            var response = client.PostAsync("https://gw.freepay.dk/api/payment", new StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            responseString = JObject.Parse(responseString).SelectToken("paymentWindowLink").Value<string>();

            return responseString;
        }

        private Dictionary<string, object> GetAddressJson(PurchaseOrder order, bool isBilling = false)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();

            if (isBilling)
            {
                string address1 = order.BillingAddress.AddressLines();
                string address2 = "";
                if (address1.Length > 50)
                {
                    address1 = address1.Substring(0, 50);
                    address2 = address1.Substring(50, 50);
                }

                values = new Dictionary<string, object>
                {
                    { "AddressLine1", address1 },
                    { "AddressLine2", address2 },
                    { "City", order.BillingAddress.City },
                    { "PostCode", order.BillingAddress.PostalCode },
                    { "Country", ISO3166.FromAlpha2(order.BillingAddress.Country.TwoLetterISORegionName).NumericCode },
                };
            }
            else
            {
                var shippingAddress = order.GetShippingAddress("Shipment");
                if (shippingAddress == null)
                {
                    shippingAddress = order.BillingAddress;
                }

                if (shippingAddress != null)
                {
                    string address1 = shippingAddress.AddressLines();
                    string address2 = "";
                    if (address1.Length > 50)
                    {
                        address1 = address1.Substring(0, 50);
                        address2 = address1.Substring(50, 50);
                    }

                    values = new Dictionary<string, object>
                        {
                            { "AddressLine1", address1 },
                            { "AddressLine2", address2 },
                            { "City", shippingAddress.City },
                            { "PostCode", shippingAddress.PostalCode },
                            { "Country", ISO3166.FromAlpha2(order.BillingAddress.Country.TwoLetterISORegionName).NumericCode },
                        };
                }
            }

            return values;
        }

        /// <summary>
        /// Adds the language to the <see cref="StringBuilder"/>
        /// </summary>
        protected virtual string GetTwoLetterLanguageCode(PaymentRequest paymentRequest)
        {
            var culture = paymentRequest.Payment != null && paymentRequest.Payment.PurchaseOrder != null &&
              paymentRequest.PurchaseOrder.CultureCode != null
                  ? new CultureInfo(paymentRequest.Payment.PurchaseOrder.CultureCode)
                  : new CultureInfo("en-us");

            return culture.TwoLetterISOLanguageName;
        }

        protected virtual string GetFourLetterLanguageCode(PaymentRequest paymentRequest)
        {
            var culture = paymentRequest.Payment != null && paymentRequest.Payment.PurchaseOrder != null &&
              paymentRequest.PurchaseOrder.CultureCode != null
                  ? new CultureInfo(paymentRequest.Payment.PurchaseOrder.CultureCode)
                  : new CultureInfo("en-us");

            var c = CultureInfo.GetCultures(CultureTypes.SpecificCultures).ToLookup(x => x.EnglishName);
            var ci = c.FirstOrDefault(x => x.Key.StartsWith(culture.EnglishName)).FirstOrDefault();
            return ci.Name;
        }
    }
}

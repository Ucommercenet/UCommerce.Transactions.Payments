using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.Configuration;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.Schibsted
{
    public class SchibstedRecurringPaymentMethodService : SchibstedPaymentMethodService
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;
        private OAuthToken UserToken { get; set; }
        private OAuthToken ServerToken { get; set; }
        private SchibstedUtil SchibstedUtil { get; set; }

        public SchibstedRecurringPaymentMethodService(
            CommerceConfigurationProvider configProvider,
            SchibstedPageBuilder pageBuilder,
            SchibstedSha256Computer sha256Computer,
            ILoggingService loggingService,
			IAbsoluteUrlService absoluteUrlService) : base(configProvider, pageBuilder, sha256Computer, loggingService, absoluteUrlService)
        {
	        _absoluteUrlService = absoluteUrlService;
        }

	    public override string RenderPage(PaymentRequest paymentRequest)
        {
            return PageBuilder.Build(paymentRequest);
        }


        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {
            var recurringPaymentRequest = ThrowInvalidOperationExceptionIfNotARecurringPaymentRequest(paymentRequest);

            if (recurringPaymentRequest.Payment == null)
            {
                var payment = recurringPaymentRequest.PurchaseOrder.Payments.FirstOrDefault();
                recurringPaymentRequest.Payment = payment ?? CreatePayment(recurringPaymentRequest);
            }

            var schibstedProduct = CreateSchibstedProduct(recurringPaymentRequest);

            recurringPaymentRequest.Payment["schibstedProductId"] = schibstedProduct.Data.ProductId.ToString();
            recurringPaymentRequest.Payment.Save();

            return base.RequestPayment(recurringPaymentRequest);
        }
        
        public override void ProcessCallback(Payment payment)
        {
			var paymentMethod = payment.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;
			string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;
			string acceptUrl = paymentMethod.DynamicProperty<string>().AcceptUrl;
			
			var schibstedOrderId = HttpContext.Current.Request["order_id"];
			ServerToken = SchibstedUtil.GetServerToken(clientId, clientSecret);
			UserToken = SchibstedUtil.GetUserToken(clientId, clientSecret,
                HttpContext.Current.Request["code"]);

            try
            {
                // Preparing for callback validation
                var orderCheck = Sha256Computer.ComputeHash(
                    payment.ReferenceId + payment.PurchaseOrder.OrderLines.Count + payment.Amount,
                    clientSecret, true);

                // Get the status object for the order, for the validation
                var orderStatus = SchibstedUtil.SchibstedApiGet<Order>("/order/" + schibstedOrderId + "/status", ServerToken.AccessToken);

                // Set initial payment status
                payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Declined);
                payment.TransactionId = schibstedOrderId;

                // Compare the checksum, and set payment status
                if (orderCheck != orderStatus.Data.ClientReference)
                    throw new SecurityException("Checksum mismatch");

                payment.PaymentStatus = SchibstedUtil.GetPaymentStatusFromOrderStatus(orderStatus.Data.Status);

                Uri redirectUrl;
                if (payment.PaymentStatus != PaymentStatus.Get((int) PaymentStatusCode.Declined))
                {
                    ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

                    // Changing order status to completed, if necessary
                    if (payment.PaymentStatus == PaymentStatus.Get((int) PaymentStatusCode.Acquired))
                        new OrderService().ChangeOrderStatus(
                            payment.PurchaseOrder,
                            OrderStatus.Get((int) OrderStatusCode.CompletedOrder));

                    // Setting up subscription
                    CreateSchibstedSubscription(Convert.ToInt32(UserToken.UserId),
                        Convert.ToInt32(payment["schibstedProductId"]));

                    // Redirecting to the cancel page
                    redirectUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl));
                }
                else
                {
                    // Redirecting to the cancel page
					redirectUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl));
                }

                redirectUrl = redirectUrl.AddOrderGuidParameter(payment.PurchaseOrder)
                    .AddQueryStringParameter("order_id", schibstedOrderId);

                HttpContext.Current.Response.Redirect(redirectUrl.AbsoluteUri);
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                throw new Exception("API Error, see log for details");
            }
        }

        private void CreateSchibstedSubscription(int userId, int productId)
        {
            var postValues = new Dictionary<string, string>()
            {
                {"oauth_token", ServerToken.AccessToken},
                {"productId", productId.ToString()}
            };

            try
            {
                SchibstedUtil.SchibstedApiPost<object>("/user/" + userId + "/subscription", postValues);
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                throw new Exception("API Error, see log for details");
            }
        }

        private SppContainer<Product> CreateSchibstedProduct(RecurringPaymentRequest paymentRequest)
        {
			var paymentMethod = paymentRequest.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;
			string paymentOptions = paymentMethod.DynamicProperty<string>().PaymentOptions;

			ServerToken = SchibstedUtil.GetServerToken(clientId, clientSecret);

            var productName = string.Join(", ", paymentRequest.PurchaseOrder.OrderLines.Select(x => x.ProductName));
            if (productName.Length > 64) productName = productName.Substring(0, 64);

            var postValues = new Dictionary<string, string>()
                {
                    {"oauth_token", ServerToken.AccessToken},
                    {"type", "2"},
                    {"code", paymentRequest.Payment.ReferenceId},
                    {"name", HttpUtility.UrlEncode(productName)},
                    {"description", "Subscription: " + paymentRequest.Payment.ReferenceId},
                    {"price", Convert.ToInt32(paymentRequest.PurchaseOrder.OrderTotal*100).ToString()},
                    {"vat", Convert.ToInt32(paymentRequest.PaymentMethod.FeePercent * 10000).ToString()},
                    {"currency", paymentRequest.PurchaseOrder.BillingCurrency.ISOCode},
                    {"paymentOptions", paymentOptions},
                    {"subscriptionPeriod", GetSubscriptionPeriod(paymentRequest).ToString()},
                    {"subscriptionAutoRenew", paymentRequest.Recurs ? "1" : "0"}
                };

            try
            {
                return SchibstedUtil.SchibstedApiPost<Product>("/product", postValues);
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                throw new Exception("API Error, see log for details");
            }
        }

        private int GetSubscriptionPeriod(RecurringPaymentRequest paymentRequest)
        {
            var durationInSeconds = 0;
            switch (paymentRequest.DurationUnit)
            {
                case DurationUnit.Day:
                    durationInSeconds = paymentRequest.DurationBetweenEachRecurrence * 86400;
                    break;
                case DurationUnit.Week:
                    durationInSeconds = paymentRequest.DurationBetweenEachRecurrence * 604800;
                    break;
                case DurationUnit.Month:
                    var toDate = paymentRequest.EffectiveFrom.AddMonths(paymentRequest.DurationBetweenEachRecurrence);
                    durationInSeconds = Convert.ToInt32(toDate.Subtract(paymentRequest.EffectiveFrom).TotalSeconds);
                    break;
                case DurationUnit.Year:
                    var start = new DateTime(paymentRequest.EffectiveFrom.Year, paymentRequest.EffectiveFrom.Month, paymentRequest.EffectiveFrom.Day);
                    var end = start.AddYears(paymentRequest.DurationBetweenEachRecurrence);
                    durationInSeconds = Convert.ToInt32(end.Subtract(start).TotalSeconds);
                    break;
                default:
                    durationInSeconds = paymentRequest.DurationBetweenEachRecurrence;
                    break;
            }

            return durationInSeconds;
        }

        private RecurringPaymentRequest ThrowInvalidOperationExceptionIfNotARecurringPaymentRequest(PaymentRequest request)
        {
            var recurringRequest = request as RecurringPaymentRequest;
            if (recurringRequest == null) throw new InvalidOperationException("Recurring payment request required for the Schibsted Recurring Payment Method Service. Please ensure that you use a RecurringPaymentRequest with this provider.");

            return recurringRequest;
        }

        private void LogWebException(WebException ex)
        {
            // Logging JSON error response
            var sr = new StreamReader(ex.Response.GetResponseStream());
            LoggingService.Log<SchibstedPaymentMethodService>("JSON Error Response: " + sr.ReadToEnd());
        }
    }
}

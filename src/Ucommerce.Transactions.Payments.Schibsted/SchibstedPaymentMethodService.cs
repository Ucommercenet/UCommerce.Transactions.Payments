using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.Schibsted
{
    public class SchibstedPaymentMethodService : ExternalPaymentMethodService
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;
        protected SchibstedSha256Computer Sha256Computer { get; set; }
        protected ILoggingService LoggingService { get; set; }
        protected SchibstedPageBuilder PageBuilder { get; set; }

        public SchibstedPaymentMethodService(SchibstedPageBuilder pageBuilder, SchibstedSha256Computer sha256Computer, ILoggingService loggingService, IAbsoluteUrlService absoluteUrlService)
        {
	        _absoluteUrlService = absoluteUrlService;
            PageBuilder = pageBuilder;
            Sha256Computer = sha256Computer;
            LoggingService = loggingService;
        }

		protected SchibstedUtil GetSchibstedUtil(PaymentMethod paymentMethod)
		{
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			return new SchibstedUtil(testMode);
		}

        public override string RenderPage(PaymentRequest paymentRequest)
        {
            return PageBuilder.Build(paymentRequest);
        }

        public override void ProcessCallback(Payment payment)
        {
			var paymentMethod = payment.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;

            var schibstedOrderId = HttpContext.Current.Request["order_id"];
	        var schibstedUtil = GetSchibstedUtil(payment.PaymentMethod);
	        var serverToken = schibstedUtil.GetServerToken(clientId, clientSecret);

            var isStatusCallback = !string.IsNullOrEmpty(payment["isStatusCallback"]) && payment["isStatusCallback"] == "true";
            if (isStatusCallback)
                schibstedOrderId = payment.TransactionId;

            try
            {
                // Preparing for callback validation
                var orderCheck = Sha256Computer.ComputeHash(
                    payment.ReferenceId + payment.PurchaseOrder.OrderLines.Count + payment.Amount,
                    clientSecret, true);

                // Get the status object for the order, for the validation
				var orderStatus = schibstedUtil.SchibstedApiGet<Order>("/order/" + schibstedOrderId + "/status", serverToken.AccessToken);

                if (isStatusCallback)
                    ProcessStatusCallback(orderCheck, payment, orderStatus);
                else
                    ProcessOrderPayment(schibstedOrderId, orderCheck, payment, orderStatus);
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                throw new Exception("API Error, see log for details");
            }
        }

        private void ProcessOrderPayment(string schibstedOrderId, string orderCheck, Payment payment, SppContainer<Order> orderStatus)
        {
			var paymentMethod = payment.PaymentMethod;
			string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;
			string acceptUrl = paymentMethod.DynamicProperty<string>().AcceptUrl;

            // Save order id on payment
            payment.TransactionId = schibstedOrderId;

            // Compare the checksum, and set payment status
            if (orderCheck != orderStatus.Data.ClientReference && (orderStatus.Data.ClientReference != null && payment.PurchaseOrder.OrderTotal > 0))
                throw new SecurityException("Checksum mismatch");
            
            // Process payment
			var schibstedUtil = GetSchibstedUtil(payment.PaymentMethod);

			payment.PaymentStatus = schibstedUtil.GetPaymentStatusFromOrderStatus(orderStatus.Data.Status);

            Uri redirectUrl;
            if(payment.PaymentStatus != PaymentStatus.Get((int) PaymentStatusCode.Declined))
            {
                ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

                // Changing order status to completed, if necessary
                if (payment.PaymentStatus == PaymentStatus.Get((int) PaymentStatusCode.Acquired))
                    new OrderService().ChangeOrderStatus(
                        payment.PurchaseOrder,
                        OrderStatus.Get((int) OrderStatusCode.CompletedOrder));
            
                // Redirecting to the confirmation page
				redirectUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl));
            }
            else
            {
                redirectUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl));
            }

            redirectUrl = redirectUrl.AddOrderGuidParameter(payment.PurchaseOrder)
                .AddQueryStringParameter("order_id", schibstedOrderId)
                .AddQueryStringParameter("code", HttpContext.Current.Request["code"]);

            HttpContext.Current.Response.Redirect(redirectUrl.AbsoluteUri);
        }

        private void ProcessStatusCallback(string orderCheck, Payment payment, SppContainer<Order> orderStatus)
        {
            if (orderCheck == orderStatus.Data.ClientReference)
            {
                var isStatusCallbackProperty = payment.PaymentProperties.SingleOrDefault(x => x.Key == "isStatusCallback");
                if(isStatusCallbackProperty != null)
                    payment.RemovePaymentProperty(isStatusCallbackProperty);

                payment.PaymentStatus = GetSchibstedUtil(payment.PaymentMethod).GetPaymentStatusFromOrderStatus(orderStatus.Data.Status);

                var orderService = new OrderService();

                switch (payment.PaymentStatus.PaymentStatusId)
                {
                    case (int)PaymentStatusCode.Acquired:
                        orderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.CompletedOrder));
                        break;
                    case (int)PaymentStatusCode.Cancelled:
                        orderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.Cancelled));
                        break;
                    case (int)PaymentStatusCode.Refunded:
                        orderService.ChangeOrderStatus(payment.PurchaseOrder, OrderStatus.Get((int)OrderStatusCode.Cancelled));
                        break;
                }

                if (!string.IsNullOrEmpty(payment["nextChangedOrders"]))
                {
                    var changedOrders = payment["nextChangedOrders"].Split(',');
                    var nextOrder = changedOrders.FirstOrDefault();

                    if (nextOrder != null)
                    {
                        var nextPayment = Payment.SingleOrDefault(x => x.TransactionId == changedOrders.FirstOrDefault());
                        if (nextPayment != null)
                        {
                            nextPayment["isStatusCallback"] = "true";
                            nextPayment["nextChangedOrders"] = string.Join(",", changedOrders.Where(x => x != nextOrder));
                            ProcessCallback(nextPayment);
                        }
                    }
                }
            }
        }

        public override Payment Extract(HttpRequest httpRequest)
        {
            //var firstPayment = Payment.SingleOrDefault(x => x.TransactionId == statusChangedOrders.FirstOrDefault().OrderId.ToString());
			var firstPayment = Payment.All().FirstOrDefault();
			var paymentMethod = firstPayment.PaymentMethod;
			string signatureSecret = paymentMethod.DynamicProperty<string>().SignatureSecret;

            var code = HttpContext.Current.Request["code"];
            var orderId = HttpContext.Current.Request["order_id"];

            if (string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(code))
            {
                // User is redirected to this page when loggin in as a new user, from the spid page.
                HttpContext.Current.Response.Redirect(base.Extract(httpRequest)["paylinkUrl"]);
            }

            if (!httpRequest.ContentType.Contains("text/plain")) return base.Extract(httpRequest);
            
            var sr = new StreamReader(httpRequest.InputStream);
            var plainBody = sr.ReadToEnd();

			var orderCallback = GetSchibstedUtil(firstPayment.PaymentMethod).DecodeSignedRequest(plainBody, signatureSecret);
            var statusChangedOrders = orderCallback.Entries.Where(x => x.ChangedFields == "status").ToList();
			
            firstPayment["isStatusCallback"] = "true";
            firstPayment["nextChangedOrders"] = 
                string.Join(",", statusChangedOrders.Where(x => x.OrderId.ToString() != firstPayment.TransactionId)
                    .Select(x => x.OrderId).Distinct());

            return firstPayment;
        }
        
        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
			var paymentMethod = payment.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;

			var serverToken = GetSchibstedUtil(payment.PaymentMethod).GetServerToken(clientId, clientSecret);

            try
            {
				var orderCancel = GetSchibstedUtil(payment.PaymentMethod).SchibstedApiPost<Order>("/order/" + payment.TransactionId + "/cancel",
                    new Dictionary<string, string>()
                    {
                        {"oauth_token", serverToken.AccessToken}
                    });

                if (orderCancel.Data.Status == -2)
                    status = PaymentMessages.CancelSuccess + " >> " + GetCallStatusMessage(orderCancel.Data.Status);
                else
                    status = PaymentMessages.CancelFailed + " >> " + GetCallStatusMessage(orderCancel.Data.Status);

                return orderCancel.Data.Status == -2;
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.CancelFailed, GetCallStatusMessage(-1), ex.Status);
                return false;
            }
        }

        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
			var paymentMethod = payment.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;
			var serverToken = GetSchibstedUtil(payment.PaymentMethod).GetServerToken(clientId, clientSecret);

            try
            {
				var orderCapture = GetSchibstedUtil(payment.PaymentMethod).SchibstedApiPost<Order>("/order/" + payment.TransactionId + "/capture", 
                    new Dictionary<string, string>()
                    {
                        {"oauth_token", serverToken.AccessToken}
                    });

                if (orderCapture.Data.Status == 2)
                    status = PaymentMessages.AcquireSuccess + " >> " + GetCallStatusMessage(orderCapture.Data.Status);
                else
                    status = PaymentMessages.AcquireFailed + " >> " + GetCallStatusMessage(orderCapture.Data.Status);

                return orderCapture.Data.Status == 2;
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.AcquireFailed, GetCallStatusMessage(-1), ex.Status);
                return false;
            }
        }

        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
			var paymentMethod = payment.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;
			var serverToken = GetSchibstedUtil(payment.PaymentMethod).GetServerToken(clientId, clientSecret);

            // Can't refund amount of 0
            if (payment.PurchaseOrder.OrderTotal <= 0)
            {
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.RefundFailed, GetCallStatusMessage(-1), "Amount can not be 0");
                return false;
            }

            try
            {
				var orderRefund = GetSchibstedUtil(payment.PaymentMethod).SchibstedApiPost<Order>("/order/" + payment.TransactionId + "/credit",
                    new Dictionary<string, string>()
                    {
                        {"oauth_token", serverToken.AccessToken},
                        {"description", "Refund payment"}
                    });

                if (orderRefund.Data.Status == 3)
                    status = PaymentMessages.RefundSuccess + " >> " + GetCallStatusMessage(orderRefund.Data.Status);
                else
                    status = PaymentMessages.RefundFailed + " >> " + GetCallStatusMessage(orderRefund.Data.Status);

                return orderRefund.Data.Status == 3;
            }
            catch (WebException ex)
            {
                LogWebException(ex);
                status = String.Format("{0} >> {1} >> {2}", PaymentMessages.RefundFailed, GetCallStatusMessage(-1), ex.Status);
                return false;
            }
        }

        private void LogWebException(WebException ex)
        {
            // Logging JSON error response
            var sr = new StreamReader(ex.Response.GetResponseStream());
            LoggingService.Log<SchibstedPaymentMethodService>("JSON Error Response: " + sr.ReadToEnd());
        }

        private string GetCallStatusMessage(int status)
        {
            switch (status)
            {
                case -3:
                    return "Expired";
                case -2:
                    return "Cancelled";
                case -1:
                    return "Failed";
                case 0:
                    return "Created";
                case 1:
                    return "Pending";
                case 2:
                    return "Complete";
                case 3:
                    return "Credited";
                case 4:
                    return "Authorized";
                default:
                    return "Unknown";
            }
        }
    }
}

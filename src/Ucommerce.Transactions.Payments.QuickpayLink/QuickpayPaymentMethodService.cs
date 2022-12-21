using System;
using Ucommerce.EntitiesV2;
using System.Web;
using Ucommerce.Extensions;
using Ucommerce.Web;
using Ucommerce.Transactions.Payments.Common;

namespace Ucommerce.Transactions.Payments.QuickpayLink
{
    public class QuickpayPaymentMethodService : ExternalPaymentMethodService
    {
        private string ApiKey(PaymentMethod paymentMethod) => paymentMethod.DynamicProperty<string>().ApiKey;
        private string CallbackUrl(PaymentMethod paymentMethod) => paymentMethod.DynamicProperty<string>().CallbackUrl;
        private string CancelUrl(PaymentMethod paymentMethod) => paymentMethod.DynamicProperty<string>().CancelUrl;
        private string PaymentMethods(PaymentMethod paymentMethod) => paymentMethod.DynamicProperty<string>().PaymentMethods;

        private readonly ICallbackUrl _callback;
        private readonly IAbsoluteUrlService _absoluteUrlService;

        public QuickpayPaymentMethodService(ICallbackUrl callback, IAbsoluteUrlService absoluteUrlService)
        {
            _callback = callback;
            _absoluteUrlService = absoluteUrlService;
        }

        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {
            var orderPayment = paymentRequest.Payment;
            var callbackUrl = _callback.GetCallbackUrl(CallbackUrl(paymentRequest.PaymentMethod), orderPayment);
            var quickpayClient = new QuickpayServiceClient(ApiKey(paymentRequest.PaymentMethod));

            // Create payment
            var paymentDto = quickpayClient.CreatePayment(orderPayment.ReferenceId, orderPayment.PurchaseOrder.BillingCurrency.ISOCode);

            // Create payment link
            var paymentLink = quickpayClient.CreatePaymentLink(new Models.CreatePaymentLinkParams()
            {
                Id = paymentDto.Id,
                AcceptUrl = callbackUrl,
                CancelUrl = CancelUrl(paymentRequest.PaymentMethod),
                Amount = Convert.ToInt32(paymentRequest.Amount.Value.ToCents()),
                PaymentMethods = PaymentMethods(paymentRequest.PaymentMethod)
            });

            // Save payment id to Ucommerce payment
            orderPayment.TransactionId = paymentDto.Id.ToString();
            orderPayment.Save();

            HttpContext.Current.Response.Redirect(paymentLink);

            return orderPayment;
        }

        public override void ProcessCallback(Payment payment)
        {
            if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
                return;

            var quickpayClient = new QuickpayServiceClient(ApiKey(payment.PaymentMethod));
            var paymentDto = quickpayClient.GetPayment(int.Parse(payment.TransactionId));

            if (paymentDto.Accepted)
            {
                var paymentStatusAuthorized = (int)PaymentStatusCode.Authorized;
                payment.PaymentStatus = EntitiesV2.PaymentStatus.Get(paymentStatusAuthorized);
            }

            ExecutePostProcessingPipeline(payment);

            var acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;
            HttpContext.Current.Response.Redirect(
             new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl))
                .AddOrderGuidParameter(payment.PurchaseOrder).ToString(), false);
        }

        // Captures the payment
        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
            var quickpayClient = new QuickpayServiceClient(ApiKey(payment.PaymentMethod));
            return quickpayClient.CapturePayment(payment, out status);
        }

        // Cancels the payment
        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
            var quickpayClient = new QuickpayServiceClient(ApiKey(payment.PaymentMethod));
            return quickpayClient.CancelPayment(int.Parse(payment.TransactionId), out status);
        }

        // Refunds the payment
        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
            var quickpayClient = new QuickpayServiceClient(ApiKey(payment.PaymentMethod));
            return quickpayClient.RefundPayment(payment, out status);
        }

        public override string RenderPage(PaymentRequest paymentRequest)
        {
            throw new NotImplementedException("QuickPay Link does not use a local form. Use RequestPayment instead.");
        }
    }
}
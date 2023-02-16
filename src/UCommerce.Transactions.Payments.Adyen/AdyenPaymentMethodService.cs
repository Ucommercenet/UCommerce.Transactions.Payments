using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Adyen.Model.Checkout;
using Adyen.Model.Modification;
using Adyen.Model.Notification;
using Adyen.Notification;
using Adyen.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NHibernate.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Transactions.Payments.Adyen.EventHandlers;
using Ucommerce.Transactions.Payments.Adyen.Extensions;
using Ucommerce.Transactions.Payments.Adyen.Factories;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Adyen
{
    public class AdyenPaymentMethodService : ExternalPaymentMethodService
    {
        private const string WebHookContentKey = nameof(WebHookContentKey);
        private const string PaymentReferenceKey = "merchantReference";
        private const string EventCodeKey = "eventCode";

        private readonly IAdyenClientFactory _clientFactory;
        private readonly IRepository<Payment> _paymentRepository;
        private readonly IAbsoluteUrlService _absoluteUrlService;
        private readonly IList<IEventHandler> _eventHandlers;
        private readonly ILoggingService _loggingService;

        public AdyenPaymentMethodService(ILoggingService loggingService,
            IAdyenClientFactory clientFactory,
            IRepository<Payment> paymentRepository,
            IAbsoluteUrlService absoluteUrlService,
            IEventHandler[] eventHandlers)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
            _absoluteUrlService = absoluteUrlService ?? throw new ArgumentNullException(nameof(absoluteUrlService));
            _eventHandlers = eventHandlers ?? throw new ArgumentNullException(nameof(eventHandlers));
        }

        /// <summary>
        /// Extracts payment from request using the default payment gateway callback extractor.
        /// Adyen expects a response acknowledging that we have received and accepted the request. Uses HttpContext to provide that response for Adyen.
        /// In special cases a request from Adyen will not contain a reference Id, and will therefore not be a valid <see cref="Payment"/> object.
        /// We need to send the accepted response before the special request reaches our ProcessCallback and returns a http 500 response to Adyen.
        /// </summary>
        /// <param name="httpRequest"></param>
        /// <returns></returns>
        public override Payment? Extract(HttpRequest httpRequest)
        {
            var contentJson = ReadWebHookContent(httpRequest);
            var jsonObj = (JObject?)JsonConvert.DeserializeObject(contentJson);
            var reference = jsonObj?["notificationItems"]?[0]?["NotificationRequestItem"]?[PaymentReferenceKey]
                ?.Value<string>();
            var eventCode = jsonObj?["notificationItems"]?[0]?["NotificationRequestItem"]?[EventCodeKey]
                ?.Value<string>();

            if (string.IsNullOrWhiteSpace(reference) && eventCode == EventCodes.ReportAvailable)
            {
                _loggingService.Information<AdyenPaymentMethodService>("We received a report webhook");
                SendAcceptHttpResponse();
            }

            return _paymentRepository.Select(x => x.ReferenceId == reference)
                .Fetch(x => x.PurchaseOrder)
                .Fetch(x => x.PaymentMethod)
                .Fetch(x => x.PaymentStatus)
                .FirstOrDefault();
        }

        /// <summary>
        /// Performs data validation on a Payment received from the <see cref="Extract"/> method. Assigns the webhook event to one of the <see cref="IEventHandler"/> handlers.
        /// Sends a response to the PSP containing an accept message.
        /// </summary>
        public override void ProcessCallback(Payment payment)
        {
            string hmacKey = payment.PaymentMethod.DynamicProperty<string>()?
                .HmacKey ?? payment.PaymentMethod.DynamicProperty<string>()?.hmacKey ?? string.Empty;
            var hmacValidator = new HmacValidator();
            var notificationHandler = new NotificationHandler();
            var contentJson = ReadWebHookContent(HttpContext.Current.Request);
            var notificationRequest = notificationHandler.HandleNotificationRequest(contentJson);

            foreach (var notificationRequestItemContainer in notificationRequest.NotificationItemContainers)
            {
                var notificationItem = notificationRequestItemContainer.NotificationItem;
                if (!hmacValidator.IsValidHmac(notificationItem, hmacKey))
                {
                    _loggingService.Information<AdyenPaymentMethodService>("The provided HMAC key for {notificationItem} is not valid.", notificationItem);
                    continue;
                }

                var handler = _eventHandlers.LastOrDefault(eh => eh.CanHandle(notificationItem.EventCode));
                if (handler is null)
                {
                    _loggingService.Information<AdyenPaymentMethodService>(
                        "An appropriate handler for {EVENT_CODE} was not found.", notificationItem.EventCode);
                    continue;
                }

                if (!notificationItem.Success)
                {
                    _loggingService.Information<AdyenPaymentMethodService>("Request unsuccessful");
                    continue;
                }

                handler.Handle(notificationItem, payment);
            }

            SendAcceptHttpResponse();
        }

        public override string RenderPage(PaymentRequest paymentRequest)
        {
            throw new NotSupportedException("Adyen does not need a local form. Use RequestPayment instead.");
        }

        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {
            if (paymentRequest.Payment == null)
            {
                paymentRequest.Payment = CreatePayment(paymentRequest);
            }

            var metadata = new Dictionary<string, string>
            {
                { "orderReference", paymentRequest.Payment.ReferenceId },
                { "orderId", paymentRequest.PurchaseOrder.Guid.ToString("D") },
                { "orderNumber", paymentRequest.PurchaseOrder.OrderNumber }
            };


            string merchantAccount = paymentRequest.PaymentMethod.DynamicProperty<string>()
                ?
                .MerchantAccount ?? string.Empty;
            string returnUrl = _absoluteUrlService.GetAbsoluteUrl(paymentRequest.PaymentMethod.DynamicProperty<string>()
                ?
                .ReturnUrl) ?? string.Empty;

            // Create a payment request
            var amount = new Amount(paymentRequest.PurchaseOrder.BillingCurrency.ISOCode,
                Convert.ToInt64(paymentRequest.Amount.Value * 100));

            var adyenPaymentRequest = new CreatePaymentLinkRequest(amount: amount, merchantAccount: merchantAccount,
                reference: paymentRequest.Payment.ReferenceId)
            {
                ReturnUrl = returnUrl,
                ShopperEmail = paymentRequest.PurchaseOrder.Customer?.EmailAddress,
                ShopperReference = paymentRequest.PurchaseOrder.Customer?.Guid.ToString(),
                ShopperName = new Name(paymentRequest.PurchaseOrder.BillingAddress?.FirstName,
                    paymentRequest.PurchaseOrder.BillingAddress?.LastName),
                CountryCode = paymentRequest.PurchaseOrder.BillingAddress?.Country.Culture.Split('-')
                    .Last(),
                Metadata = metadata
            };

            var checkout = _clientFactory.GetCheckout(paymentRequest.PaymentMethod);
            var result = checkout.PaymentLinks(adyenPaymentRequest);

            if (string.IsNullOrWhiteSpace(result.Url))
            {
                throw new InvalidOperationException("Could not redirect to Adyen payment page.");
            }

            HttpContext.Current.Response.Redirect(result.Url);

            return paymentRequest.Payment;
        }

        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
            var amount = new global::Adyen.Model.Amount(payment.PurchaseOrder.BillingCurrency.ISOCode,
                Convert.ToInt64(payment.Amount * 100));
            string merchantAccount = payment.PaymentMethod.DynamicProperty<string>()
                ?
                .MerchantAccount ?? string.Empty;

            var modification = _clientFactory.GetModification(payment.PaymentMethod);
            var result = modification.Capture(new CaptureRequest
            {
                MerchantAccount = merchantAccount,
                ModificationAmount = amount,
                OriginalReference = payment.TransactionId
            });

            status = result.Status;

            if (result.Response == global::Adyen.Model.Enum.ResponseEnum.CaptureReceived)
            {
                return true;
            }

            return false;
        }

        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
            var amount = new global::Adyen.Model.Amount(payment.PurchaseOrder.BillingCurrency.ISOCode,
                Convert.ToInt64(payment.Amount * 100));
            string merchantAccount = payment.PaymentMethod.DynamicProperty<string>()
                ?
                .MerchantAccount ?? string.Empty;

            var modification = _clientFactory.GetModification(payment.PaymentMethod);
            var result = modification.Refund(new RefundRequest
            {
                MerchantAccount = merchantAccount,
                ModificationAmount = amount,
                OriginalReference = payment.TransactionId
            });

            status = result.Status;

            if (result.Response == global::Adyen.Model.Enum.ResponseEnum.RefundReceived ||
                result.Response == global::Adyen.Model.Enum.ResponseEnum.CancelOrRefundReceived)
            {
                return true;
            }

            return false;
        }

        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
            string merchantAccount = payment.PaymentMethod.DynamicProperty<string>()
                ?
                .MerchantAccount ?? string.Empty;

            var modification = _clientFactory.GetModification(payment.PaymentMethod);

            var result = modification.Cancel(new CancelRequest
            {
                MerchantAccount = merchantAccount,
                OriginalReference = payment.TransactionId
            });

            status = result.Status;

            if (result.Response == global::Adyen.Model.Enum.ResponseEnum.CancelReceived ||
                result.Response == global::Adyen.Model.Enum.ResponseEnum.CancelOrRefundReceived)
            {
                return true;
            }

            return false;
        }

        private string ReadWebHookContent(HttpRequest httpRequest)
        {
            if (HttpContext.Current.Items.Contains(WebHookContentKey))
                return HttpContext.Current.Items[WebHookContentKey] as string;

            Stream inputStream = httpRequest.GetBufferedInputStream();
            using var reader = new StreamReader(inputStream, Encoding.UTF8);

            var webHookContent = reader.ReadToEnd();
            HttpContext.Current.Items.Add(WebHookContentKey, webHookContent);
            return webHookContent;
        }

        protected virtual void SendAcceptHttpResponse()
        {
            HttpContext.Current?.Response.Write("[accepted]");
            HttpContext.Current?.Response.End();
        }
    }
}
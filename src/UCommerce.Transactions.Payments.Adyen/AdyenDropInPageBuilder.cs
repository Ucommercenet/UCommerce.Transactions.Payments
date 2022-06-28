using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Adyen.Model.Checkout;
using Ucommerce.Transactions.Payments.Adyen.Extensions;
using Ucommerce.Transactions.Payments.Adyen.Factories;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Adyen
{
    public class AdyenDropInPageBuilder : AbstractPageBuilder
    {
        private readonly IAdyenClientFactory _adyenClientFactory;
        private readonly ICallbackUrl _callbackUrl;

        public AdyenDropInPageBuilder(ICallbackUrl callbackUrl, IAdyenClientFactory adyenClientFactory)
        {
            _callbackUrl = callbackUrl ?? throw new ArgumentNullException(nameof(callbackUrl));
            _adyenClientFactory = adyenClientFactory ?? throw new ArgumentNullException(nameof(adyenClientFactory));
        }

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
            bool liveMode = paymentRequest.PaymentMethod.DynamicProperty<bool>()?
                                          .Live ?? false;
            string clientKey = paymentRequest.PaymentMethod.DynamicProperty<string>()?
                                             .ClientKey ?? string.Empty;

            var checkoutSessionResponse = CreateCheckoutSession(paymentRequest);

            string paymentFormTemplate = paymentRequest.PaymentMethod.DynamicProperty<string>()?
                                                       .PaymentFormTemplate ?? string.Empty;
            var allLines = File.ReadAllLines(HttpContext.Current.Server.MapPath(paymentFormTemplate));
            foreach (var line in allLines)
            {
                page.AppendLine(line);
            }

            page.Replace("##ADYEN:ENVIRONMENT##", liveMode ? "live" : "test");
            page.Replace("##ADYEN:CLIENTKEY##", clientKey);
            page.Replace("##ADYEN:SESSION##", checkoutSessionResponse.Id);
            page.Replace("##ADYEN:SESSIONDATA##", checkoutSessionResponse.SessionData);
        }

        private CreateCheckoutSessionResponse CreateCheckoutSession(PaymentRequest paymentRequest)
        {
            string merchantAccount = paymentRequest.PaymentMethod.DynamicProperty<string>()?
                                                   .MerchantAccount ?? string.Empty;
            string callBackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>()?
                                               .CallbackUrl ?? string.Empty;

            var checkoutSessionRequest = new CreateCheckoutSessionRequest
            {
                MerchantAccount = merchantAccount,
                Reference = paymentRequest.Payment.ReferenceId,
                ReturnUrl = _callbackUrl.GetCallbackUrl(callBackUrl, paymentRequest.Payment),
                Amount = new Amount(paymentRequest.Amount.CurrencyIsoCode,
                                    Convert.ToInt64(paymentRequest.Amount.Value * 100)),
                CountryCode = paymentRequest.PurchaseOrder.BillingAddress.Country.Culture.Split('-')
                                            .Last(),
            };

            var checkout = _adyenClientFactory.GetCheckout(paymentRequest.PaymentMethod);
            return checkout.Sessions(checkoutSessionRequest);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Adyen;
using Adyen.Model.Checkout;
using Adyen.Service;
using Ucommerce.Extensions;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Adyen
{
    public class AdyenDropInPageBuilder : AbstractPageBuilder
    {
        private readonly ICallbackUrl _callbackUrl;

        public AdyenDropInPageBuilder(ICallbackUrl callbackUrl)
        {
            _callbackUrl = callbackUrl;
        }

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
            bool liveMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().Live;
            string clientKey = paymentRequest.PaymentMethod.DynamicProperty<string>().ClientKey;

            var checkoutSessionResponse = CreateCheckoutSession(paymentRequest, liveMode);

            string paymentFormTemplate = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentFormTemplate;
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

        private CreateCheckoutSessionResponse CreateCheckoutSession(PaymentRequest paymentRequest, bool liveMode)
        {
            string apiKey = paymentRequest.PaymentMethod.DynamicProperty<string>().ApiKey;
            string merchantAccount = paymentRequest.PaymentMethod.DynamicProperty<string>().MerchantAccount;
            string callBackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;

            var checkoutSessionRequest = new CreateCheckoutSessionRequest
            {
                MerchantAccount = merchantAccount,
                Reference = paymentRequest.Payment.ReferenceId,
                ReturnUrl = _callbackUrl.GetCallbackUrl(callBackUrl, paymentRequest.Payment),
                Amount = new Amount(paymentRequest.Amount.CurrencyIsoCode, Convert.ToInt64(paymentRequest.Amount.Value * 100)),
                CountryCode = paymentRequest.PurchaseOrder.BillingAddress.Country.Culture.Split('-').Last(),
                
            };

            var client = liveMode ? new Client(HttpUtility.UrlDecode(apiKey), global::Adyen.Model.Enum.Environment.Live) : new Client(HttpUtility.UrlDecode(apiKey), global::Adyen.Model.Enum.Environment.Test);
            
            var checkout = new Checkout(client);
            return checkout.Sessions(checkoutSessionRequest);
        }
    }
}

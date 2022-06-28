using System.Web;
using Adyen;
using Adyen.Model.Enum;
using Adyen.Service;
using Ucommerce.EntitiesV2;
using Ucommerce.Transactions.Payments.Adyen.Extensions;

namespace Ucommerce.Transactions.Payments.Adyen.Factories
{
    public class AdyenClientFactory : IAdyenClientFactory
    {
        public Checkout GetCheckout(PaymentMethod paymentMethod)
        {
            var client = GetClient(paymentMethod);
            return new Checkout(client);
        }

        public Client GetClient(PaymentMethod paymentMethod)
        {
            string apiKey = paymentMethod.DynamicProperty<string>()
                                      ?.ApiKey ?? string.Empty;
            bool liveMode = paymentMethod.DynamicProperty<bool>()
                                        ?.Live ?? false;


            return  new Client(HttpUtility.UrlDecode(apiKey), liveMode ? Environment.Live : Environment.Test);
        }
    }
}

using Adyen;
using Adyen.Model.Enum;
using Adyen.Service;
using Ucommerce.EntitiesV2;
using Ucommerce.Transactions.Payments.Adyen.Extensions;

namespace Ucommerce.Transactions.Payments.Adyen.Factories
{
    public class AdyenClientFactory : IAdyenClientFactory
    {
        public virtual Checkout GetCheckout(PaymentMethod paymentMethod)
        {
            var client = GetClient(paymentMethod);
            return new Checkout(client);
        }

        public virtual Modification GetModification(PaymentMethod paymentMethod)
        {
            var client = GetClient(paymentMethod);
            return new Modification(client);
        }

        private Client GetClient(PaymentMethod paymentMethod)
        {
            string apiKey = paymentMethod.DynamicProperty<string>()
                                      ?.ApiKey ?? string.Empty;
            bool liveMode = paymentMethod.DynamicProperty<bool>()
                                        ?.Live ?? false;
            string? liveEndpointUrlPrefix = paymentMethod.DynamicProperty<string?>()
                ?.LiveEndpointUrlPrefix ?? null;


            return  new Client(apiKey, liveMode ? Environment.Live : Environment.Test, liveEndpointUrlPrefix);
        }
    }
}

using Adyen;
using Adyen.Service;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.Factories
{
    public interface IAdyenClientFactory
    {
        Checkout GetCheckout(PaymentMethod paymentMethod);
        Client GetClient(PaymentMethod paymentMethod);
    }
}

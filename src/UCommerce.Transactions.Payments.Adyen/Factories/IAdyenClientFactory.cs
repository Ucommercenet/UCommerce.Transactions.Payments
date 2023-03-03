using Adyen.Service;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.Factories
{
    public interface IAdyenClientFactory
    {
        Checkout GetCheckout(PaymentMethod paymentMethod);
        Modification GetModification(PaymentMethod paymentMethod);
    }
}

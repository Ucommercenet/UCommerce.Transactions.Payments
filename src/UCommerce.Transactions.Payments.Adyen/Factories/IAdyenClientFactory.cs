using Adyen.HttpClient.Interfaces;

namespace Ucommerce.Transactions.Payments.Adyen.Factories
{
    public interface IAdyenClientFactory
    {
        IClient GetClient();
    }
}

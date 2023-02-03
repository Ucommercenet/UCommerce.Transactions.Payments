using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers
{
    public interface IEventHandler
    {
        bool CanHandle(string eventCode);
        void Handle(NotificationRequestItem notification, Payment payment);
    }
}


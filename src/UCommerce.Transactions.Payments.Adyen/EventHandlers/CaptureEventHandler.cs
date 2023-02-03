using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

public class CaptureEventHandler: IEventHandler
{
    public bool CanHandle(string eventCode)
    {
        throw new System.NotImplementedException();
    }

    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        throw new System.NotImplementedException();
    }
}
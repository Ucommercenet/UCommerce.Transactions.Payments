using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

public class CancellationEventHandler: IEventHandler
{
    public bool CanHandle(string eventCode)
    {
        if (eventCode == "CANCELLATION")
        {
            return true;
        }

        return false;
    }

    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Cancelled);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
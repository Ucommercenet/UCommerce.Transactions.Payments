using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

public class CaptureFailedEventHandler: IEventHandler
{
    public bool CanHandle(string eventCode)
    {
        if (eventCode == "CAPTURE_FAILED")
        {
            return true;
        }

        return false;
    }

    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.AcquireFailed);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
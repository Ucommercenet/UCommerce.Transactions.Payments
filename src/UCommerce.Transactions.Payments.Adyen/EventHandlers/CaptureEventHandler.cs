using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for Capture events.
/// </summary>
public class CaptureEventHandler: IEventHandler
{
    /// <inheritdoc />
    public bool CanHandle(string eventCode)
    {
        if (eventCode == EventCodes.Capture)
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
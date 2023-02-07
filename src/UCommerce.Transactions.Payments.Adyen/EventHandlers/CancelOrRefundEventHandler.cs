using System;
using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

public class CancelOrRefundEventHandler: IEventHandler
{
    public bool CanHandle(string eventCode)
    {
        if (eventCode == "CANCEL_OR_REFUND")
        {
            return true;
        }

        return false;
    }

    
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Cancelled);
        var notificationType = "refund";
        if(notification.AdditionalData.TryGetValue("modification.action",out notificationType))
        {
            payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Refunded);
        }
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
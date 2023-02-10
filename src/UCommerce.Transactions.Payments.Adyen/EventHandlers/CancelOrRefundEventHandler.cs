using System;
using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for CancelOrRefund events.
/// </summary>
public class CancelOrRefundEventHandler : IEventHandler
{
    /// <inheritdoc />
    public bool CanHandle(string eventCode)
    {
        if (eventCode == EventCodes.CancelOrRefund)
        {
            return true;
        }

        return false;
    }


    /// <inheritdoc />
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Cancelled);
        if (notification.AdditionalData.TryGetValue("modification.action", out var notificationType))
        {
            payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Refunded);
        }

        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
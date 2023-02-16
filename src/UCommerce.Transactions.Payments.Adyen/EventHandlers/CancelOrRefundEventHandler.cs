using System;
using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure.Logging;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for CancelOrRefund events.
/// </summary>
public class CancelOrRefundEventHandler : IEventHandler
{
    private readonly IRepository<PaymentStatus> _paymentStatusRepository;
    private readonly ILoggingService _loggingService;

    public CancelOrRefundEventHandler(IRepository<PaymentStatus> paymentStatusRepository,
        ILoggingService loggingService)
    {
        _paymentStatusRepository = paymentStatusRepository;
        _loggingService = loggingService;
    }

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
        if (notification.AdditionalData.TryGetValue("modification.action", out var notificationType))
        {
            payment.PaymentStatus =
                _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.Cancelled);
            if (notificationType == "refund")
            {
                payment.PaymentStatus =
                    _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.Refunded);
            }

            payment.TransactionId = notification.PspReference;
            payment.Save();
            return;
        }

        _loggingService.Information<AdyenPaymentMethodService>(
            "Ucommerce could not determine whether the {Notification_Request} was a cancellation or a refund. Payment has not been updated.", notification);
    }
}
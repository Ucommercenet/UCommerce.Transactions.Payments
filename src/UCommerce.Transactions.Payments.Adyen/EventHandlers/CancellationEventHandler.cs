using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for Cancellation events.
/// </summary>
public class CancellationEventHandler : IEventHandler
{
    private readonly IRepository<PaymentStatus> _paymentStatusRepository;
    /// <summary>
    /// CTOR for CancellationEventHandler
    /// </summary>
    public CancellationEventHandler(IRepository<PaymentStatus> paymentStatusRepository)
    {
        _paymentStatusRepository = paymentStatusRepository;
    }

    /// <inheritdoc />
    public bool CanHandle(string eventCode)
    {
        if (eventCode == EventCodes.Cancellation)
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.Cancelled);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
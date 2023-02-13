using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for Capture events.
/// </summary>
public class CaptureEventHandler : IEventHandler
{
    private readonly IRepository<PaymentStatus> _paymentStatusRepository;

    /// <summary>
    /// CTOR for CaptureEventHandler
    /// </summary>
    public CaptureEventHandler(IRepository<PaymentStatus> paymentStatusRepository)
    {
        _paymentStatusRepository = paymentStatusRepository;
    }

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
        payment.PaymentStatus = _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.Acquired);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
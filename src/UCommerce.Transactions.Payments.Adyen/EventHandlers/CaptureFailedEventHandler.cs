using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for CaptureFailed events.
/// </summary>
public class CaptureFailedEventHandler: IEventHandler
{
    
    private readonly IRepository<PaymentStatus> _paymentStatusRepository;

    /// <summary>
    /// CTOR for CaptureFailedEventHandler
    /// </summary>
    public CaptureFailedEventHandler(IRepository<PaymentStatus> paymentStatusRepository)
    {
        _paymentStatusRepository = paymentStatusRepository;
    }

    /// <inheritdoc />
    public bool CanHandle(string eventCode)
    {
        if (eventCode == EventCodes.CaptureFailed)
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.AcquireFailed);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for Refund events.
/// </summary>
public class RefundEventHandler : IEventHandler

{
    private readonly IRepository<PaymentStatus> _paymentStatusRepository;
    /// <summary>
    /// CTOR for RefundEventHandler
    /// </summary>
    public RefundEventHandler(IRepository<PaymentStatus> paymentStatusRepository)
    {
        _paymentStatusRepository = paymentStatusRepository;
    }

    /// <inheritdoc />
    public bool CanHandle(string eventCode)
    {
        if (eventCode == EventCodes.Refund)
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.Refunded);
        payment.TransactionId = notification.PspReference;
        payment.Save();
    }
}
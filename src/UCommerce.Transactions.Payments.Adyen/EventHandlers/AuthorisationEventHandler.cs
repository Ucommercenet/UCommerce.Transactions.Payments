using System;
using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;
using Ucommerce.Pipelines;
using Ucommerce.Transactions.Payments;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

/// <summary>
/// EventHandler for Authorisation events.
/// </summary>
public class AuthorisationEventHandler : IEventHandler
{
    private readonly IRepository<PaymentStatus> _paymentStatusRepository;
    /// <summary>
    /// CTOR for AuthorisationEventHandler
    /// </summary>
    public AuthorisationEventHandler(IRepository<PaymentStatus> paymentStatusRepository)
    {
        _paymentStatusRepository = paymentStatusRepository;
    }

    /// <inheritdoc />
    public bool CanHandle(string eventCode)
    {
        if (eventCode == EventCodes.Authorisation)
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = _paymentStatusRepository.SingleOrDefault(status => status.PaymentStatusId == (int)PaymentStatusCode.Authorized);
        payment.TransactionId = notification.PspReference;
        payment.Save();

        if (!string.IsNullOrWhiteSpace(payment.PaymentMethod.Pipeline))
        {
            var factory = PipelineFactory.Create<PurchaseOrder>(payment.PaymentMethod.Pipeline)
                .Execute(payment.PurchaseOrder);
            if (factory != PipelineExecutionResult.Success)
            {
                throw new PipelineException("Ucommerce was not able to successfully run the checkout pipeline.");
            }
        }
    }
}
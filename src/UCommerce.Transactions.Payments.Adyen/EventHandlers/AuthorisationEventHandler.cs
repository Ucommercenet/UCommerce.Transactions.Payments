using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;
using Ucommerce.Pipelines;
using Ucommerce.Transactions.Payments;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers;

public class AuthorisationEventHandler : IEventHandler
{
    public bool CanHandle(string eventCode)
    {
        if (eventCode == "AUTHORISATION")
        {
            return true;
        }

        return false;
    }

    public void Handle(NotificationRequestItem notification, Payment payment)
    {
        payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
        payment.TransactionId = notification.PspReference;
        payment.Save();

        if (string.IsNullOrWhiteSpace(payment.PaymentMethod.Pipeline))
        {
            var factory = PipelineFactory.Create<PurchaseOrder>(payment.PaymentMethod.Pipeline)
                .Execute(payment.PurchaseOrder);
            if (factory != PipelineExecutionResult.Success)
            {
                throw new PipelineException("Ucommerce was not able to succesfully run the checkout pipeline.");
            }
        }
    }
}
using Adyen.Model.Notification;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Adyen.EventHandlers
{
    /// <summary>
    /// Generic interface for classes that can handle various webhook event code requests.
    /// </summary>
    public interface IEventHandler
    {
        /// <summary>
        /// Evaluates if the handler is appropriate for the incoming type of event.
        /// </summary>
        bool CanHandle(string eventCode);
        /// <summary>
        /// Updates and saves the Payment with the updated info from the notification.
        /// </summary>
        void Handle(NotificationRequestItem notification, Payment payment);
    }
}


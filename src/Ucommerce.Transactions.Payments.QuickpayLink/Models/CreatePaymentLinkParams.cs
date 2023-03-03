namespace Ucommerce.Transactions.Payments.QuickpayLink.Models
{
    internal class CreatePaymentLinkParams
    {
        /// <summary>
        /// The ID of the payment.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The amount in cents.
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// The accept url used to redirect the customer to after authorization of payment.
        /// </summary>
        public string AcceptUrl { get; set; }

        /// <summary>
        /// The url redirected to in case the customer cancels the request.
        /// </summary>
        public string CancelUrl { get; set; }

        /// <summary>
        /// Endpoint url for async callback.
        /// </summary>
        public string CallBackUrl { get; set; }

        /// <summary>
        /// Allowed payment methods.
        /// </summary>
        public string PaymentMethods { get; set; }
    }
}

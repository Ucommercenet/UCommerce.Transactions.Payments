using System;
using System.Linq;
using System.Web;
using Ucommerce.EntitiesV2;
using Stripe;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Stripe
{
    /// <summary>
    /// Implementation of the http://www.stripe.com/ payment provider.
    /// </summary>
    /// <remarks>
    /// The flow is a little different for Stripe than other payment providers due to Stripe's PaymentIntent API
    /// The form is created with Javascript, then submitted to Stripe. Javascript handles the result, then posts
    /// to the PaymentProcessor.axd endpoint if successful.
    /// Currently this is a placeholder class to handle recurring payments.
    /// </remarks>
    public class StripeSubscriptionPaymentMethodService : ExternalPaymentMethodService
    {
        public override void ProcessCallback(Payment payment)
        {
            throw new NotImplementedException();
        }

        public override string RenderPage(PaymentRequest paymentRequest)
        {
            throw new NotImplementedException();
        }

        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
            throw new NotImplementedException();
        }

        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
            throw new NotImplementedException();
        }

        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
            throw new NotImplementedException();
        }
    }
}
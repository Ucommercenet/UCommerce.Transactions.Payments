using System;
using System.Linq;
using System.Text;
using System.Web;
using Stripe;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using UCommerce.Providers.StripeNet;
using Ucommerce.Web;
using Ucommerce.Transactions.Payments.Common;
using PaymentMethod = Ucommerce.EntitiesV2.PaymentMethod;

namespace Ucommerce.Transactions.Payments.Stripe
{
    /// <summary>
    /// Implementation of the http://www.braintreepayments.com/ payment provider.
    /// </summary>
    /// <remarks>
    /// The flow is a little different for Braintree than the rest for the framework.
    /// The form is submitted and braintree processes and calls back. The form validation results
    /// are in the callback and if any error we show the form again. This flow continues until form validation succeeds.
    /// </remarks>
    public class StripePaymentMethodService : ExternalPaymentMethodService
    {
	    public override string RenderPage(PaymentRequest paymentRequest)
	    {
		    throw new NotImplementedException();
	    }

	    public override void ProcessCallback(Payment payment)
	    {
		    throw new NotImplementedException();
	    }

	    protected override bool CancelPaymentInternal(Payment payment, out string status)
	    {
		    throw new NotImplementedException();
	    }

	    protected override bool AcquirePaymentInternal(Payment payment, out string status)
	    {
		    throw new NotImplementedException();
	    }

	    protected override bool RefundPaymentInternal(Payment payment, out string status)
	    {
		    throw new NotImplementedException();
	    }
    }
}
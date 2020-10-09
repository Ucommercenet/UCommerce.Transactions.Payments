using System;
using System.Linq;
using System.Web;
using Ucommerce.EntitiesV2;
using Stripe;
using Ucommerce.Web;

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

	    private StripePageBuilder StripePageBuilder;
	    private IStripeClient Client;
	    private PaymentIntentService PaymentIntentService;
	    private ChargeService ChargeService;
	    private RefundService RefundService;
	    private IAbsoluteUrlService _absoluteUrlService;
		private readonly string PaymentIntentKey = "paymentIntent";
		private readonly string SecretKey = "SecretKey";

	    public StripePaymentMethodService(StripePageBuilder stripePageBuilder, IAbsoluteUrlService absoluteUrlService)
	    {
		    _absoluteUrlService = absoluteUrlService;
		    StripePageBuilder = stripePageBuilder;
	    }

	    public override string RenderPage(PaymentRequest paymentRequest)
	    {
		    return StripePageBuilder.Build(paymentRequest);
	    }

	    private void InitClient(Payment payment)
	    {
		    Client = new StripeClient(payment.PaymentMethod.GetProperty(SecretKey).GetValue().ToString());
		    ChargeService = new ChargeService(Client);
		    PaymentIntentService = new PaymentIntentService(Client);
		    RefundService = new RefundService(Client);
	    }

		public override void ProcessCallback(Payment payment)
		{
			InitClient(payment);
			// BH: Normally, our payment processor would "ping" this endpoint.
			// However, we're going to do it from AJAX ourselves, thus negating the need for a Stripe webhook.
			var paymentIntentId = payment.PaymentProperties.First(p => p.Key == PaymentIntentKey).Value;

			// Just confirm the payment intent exists
			var paymentIntent = PaymentIntentService.Get(paymentIntentId);

			// Firstly: does the payment intent require manual confirmation?
			if (paymentIntent.ConfirmationMethod == "manual") {
				try {
                    paymentIntent = PaymentIntentService.Confirm(paymentIntent.Id);
                } catch {
					throw new InvalidOperationException("Could not confirm payment intent");
                }
            }

			if (paymentIntent.Status != StripeStatus.Succeeded)
				throw new InvalidOperationException("Payment intent capture not successful");

			var transaction = paymentIntent.Charges.First();

			if (transaction.Currency != payment.PurchaseOrder.BillingCurrency.ISOCode.ToLower())
				throw new InvalidOperationException($"The payment currency ({payment.PurchaseOrder.BillingCurrency.ISOCode.ToUpper()}) and the currency configured for the merchant account ({transaction.Currency.ToUpper()}) doesn't match. Make sure that the payment currency matches the currency selected in the merchant account.");

			var paymentStatus = PaymentStatusCode.Declined;
			if (paymentIntent.Status == StripeStatus.Succeeded)
			{
				if (string.IsNullOrEmpty(transaction.Id))
					throw new ArgumentException(@"Charge ID must be present in the PaymentIntent object.");
				payment.TransactionId = paymentIntent.Id; // This is used for 
				paymentStatus = PaymentStatusCode.Authorized;
			}

			payment.PaymentStatus = PaymentStatus.Get((int)paymentStatus);
			ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			HttpContext.Current.Response.StatusCode = 200;
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
	    {
			InitClient(payment);
		    var paymentIntentId = payment.PaymentProperties.First(p => p.Key == PaymentIntentKey).Value;
			var paymentIntent = PaymentIntentService.Get(paymentIntentId);
			if (paymentIntent.Status == StripeStatus.Succeeded)
				throw new InvalidOperationException("Cannot cancel a payment that has already been captured");

		    var result = PaymentIntentService.Cancel(paymentIntent.Id);
		    status = result.Status;
		    return result.Status == StripeStatus.Canceled;
	    }

	    protected override bool AcquirePaymentInternal(Payment payment, out string status)
	    {
			InitClient(payment);
			var paymentIntentId = payment.PaymentProperties.First(p => p.Key == PaymentIntentKey).Value;
		    var paymentIntent = PaymentIntentService.Get(paymentIntentId);
		    var succeeded = false;
		    switch (paymentIntent.Status)
		    {
				case StripeStatus.Canceled:
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Cancelled);
					break;
				case StripeStatus.RequiresAction:
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.PendingAuthorization);
					break;
				case StripeStatus.RequiresCapture:
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Authorized);
					break;
				case StripeStatus.RequiresConfirmation:
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.AcquireFailed);
					break;
				case StripeStatus.RequiresPaymentMethod:
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Declined);
					break;
				case StripeStatus.Succeeded:
					succeeded = true;
					payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Acquired);
					break;
				default:
					break;
		    }
		    payment.Save();
		    status = paymentIntent.Status;
		    return succeeded;
	    }

	    protected override bool RefundPaymentInternal(Payment payment, out string status)
	    {
			InitClient(payment);
			var paymentIntentId = payment.PaymentProperties.First(p => p.Key == PaymentIntentKey).Value;
			var paymentIntent = PaymentIntentService.Get(paymentIntentId);
			var refundOptions = new RefundCreateOptions() { PaymentIntent = paymentIntentId };
			var refunded = false;
			switch (paymentIntent.Status)
            {
				case StripeStatus.Succeeded:
					var refundResult = RefundService.Create(refundOptions);
					status = refundResult.Status;
					if (refundResult.Status != StripeStatus.Failed || refundResult.Status != StripeStatus.Canceled)
					{
						// In the process of being refunded, at least
						refunded = true;
						payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Refunded);
						payment.Save();
					}
					break;
				default:
					return CancelPaymentInternal(payment, out status);
			}
			return refunded;
	    }
    }
}
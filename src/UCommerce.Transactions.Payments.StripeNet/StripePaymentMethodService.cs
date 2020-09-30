using System;
using System.Linq;
using System.Web;
using Ucommerce.EntitiesV2;
using Stripe;
using Ucommerce.Extensions;
using Ucommerce.Pipelines.Transactions.Baskets.Basket;
using Ucommerce.Transactions.Payments;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.StripeNet
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
		    Client = new StripeClient(payment.PaymentMethod.GetProperty("secretKey").GetValue().ToString());
		    ChargeService = new ChargeService(Client);
		    PaymentIntentService = new PaymentIntentService(Client);
		    RefundService = new RefundService(Client);
	    }

	    public override void ProcessCallback(Payment payment)
	    {
		    string paymentFormUrl = payment.PaymentMethod.DynamicProperty<string>().PaymentFormUrl;
		    string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;
		    string declineUrl = payment.PaymentMethod.DynamicProperty<string>().DeclineUrl;
			var paymentIntent = payment.PaymentProperties.First(p => p.Key == "paymentIntent").Value;

		    var pi = new PaymentIntentCreateOptions() { Amount = Convert.ToInt64(payment.Amount) };
			// Just confirm the payment intent exists
			var confirmResult = PaymentIntentService.Confirm(paymentIntent);

		    if (confirmResult.Status != StripeStatus.Succeeded)
			    HttpContext.Current.Response.Redirect(paymentFormUrl == "(auto)"
				    ? $"/{payment.PaymentMethod.PaymentMethodId}/{payment["paymentGuid"]}/PaymentRequest.axd?errorMessage={confirmResult.Status}"
				    : $"{paymentFormUrl}?paymentGuid={payment["paymentGuid"]}&errorMessage={confirmResult.Status}");

		    var transaction = confirmResult.Charges.First();

		    if (transaction.Currency != payment.PurchaseOrder.BillingCurrency.ISOCode.ToLower())
			    throw new InvalidOperationException($"The payment currency ({payment.PurchaseOrder.BillingCurrency.ISOCode.ToUpper()}) and the currency configured for the merchant account ({transaction.Currency.ToUpper()}) doesn't match. Make sure that the payment currency matches the currency selected in the merchant account.");

		    var paymentStatus = PaymentStatusCode.Declined;
		    if (confirmResult.Status == StripeStatus.Succeeded)
		    {
			    if (string.IsNullOrEmpty(transaction.Id))
				    throw new ArgumentException(@"transactionId must be present in query string.");
			    payment.TransactionId = transaction.Id;
			    paymentStatus = PaymentStatusCode.Authorized;
		    }

		    payment.PaymentStatus = PaymentStatus.Get((int)paymentStatus);
		    ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
		    HttpContext.Current.Response.Redirect(paymentStatus == PaymentStatusCode.Authorized 
			    ? new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString() 
			    : new Uri(_absoluteUrlService.GetAbsoluteUrl(declineUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
	    }

	    protected override bool CancelPaymentInternal(Payment payment, out string status)
	    {
		    var paymentIntent = payment.PaymentProperties.First(p => p.Key == "paymentIntent").Value;
		    var result = PaymentIntentService.Cancel(paymentIntent);
		    status = result.Status;
		    return result.Status == StripeStatus.Canceled;
	    }

	    protected override bool AcquirePaymentInternal(Payment payment, out string status)
	    {
		    var paymentIntent = payment.PaymentProperties.First(p => p.Key == "paymentIntent").Value;
		    var captureResult = PaymentIntentService.Capture(paymentIntent);
		    var succeeded = false;
		    switch (captureResult.Status)
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
		    status = captureResult.Status;
		    return succeeded;
	    }

	    protected override bool RefundPaymentInternal(Payment payment, out string status)
	    {
		    var chargeId = payment.PaymentProperties.First(p => p.Key == "paymentIntent").Value;
			var refundOptions = new RefundCreateOptions() { Charge = chargeId };
			var refundResult = RefundService.Create(refundOptions);
			status = refundResult.Status;
			var refunded = false;
			if (refundResult.Status == StripeStatus.Succeeded) {
				payment.PaymentStatus = PaymentStatus.Get((int) PaymentStatusCode.Refunded);
				payment.Save();
			}
			return refunded;
	    }

	    private static class StripeStatus
	    {
		    public const string Succeeded = "succeeded";
		    public const string Canceled = "canceled";
		    public const string Processing = "processing";
		    public const string RequiresAction = "requires_action";
		    public const string RequiresCapture = "requires_capture";
		    public const string RequiresConfirmation = "requires_confirmation";
		    public const string RequiresPaymentMethod = "requires_confirmation_method";
	    }
    }
}
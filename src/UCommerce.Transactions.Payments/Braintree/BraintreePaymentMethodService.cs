using System;
using System.Text;
using System.Web;
using Braintree;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Web;
using Environment = Braintree.Environment;
using UCommerce.Transactions.Payments.Common;

namespace UCommerce.Transactions.Payments.Braintree
{
    /// <summary>
    /// Implementation of the http://www.braintreepayments.com/ payment provider.
    /// </summary>
    /// <remarks>
    /// The flow is a little different for Braintree than the rest for the framework.
    /// The form is submitted and braintree processes and calls back. The form validation results
    /// are in the callback and if any error we show the form again. This flow continues until form validation succeeds.
    /// </remarks>
    public class BraintreePaymentMethodService : ExternalPaymentMethodService
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;
	    private BraintreePageBuilder BraintreePageBuilder { get; set; }

	    private BraintreeGateway GetBraintreeGateway(PaymentMethod paymentMethod)
	    {
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			string merchantId = paymentMethod.DynamicProperty<string>().MerchantId;
			string publicKey = paymentMethod.DynamicProperty<string>().PublicKey;
			string privateKey = paymentMethod.DynamicProperty<string>().PrivateKey;
			
			return new BraintreeGateway
                            {
                                Configuration =
                                    new global::Braintree.Configuration(
                                    testMode ? Environment.SANDBOX : Environment.PRODUCTION,
                                    merchantId, publicKey, privateKey)
                            };
	    }

	    public BraintreePaymentMethodService(BraintreePageBuilder braintreePageBuilder, IAbsoluteUrlService absoluteUrlService)
	    {
		    _absoluteUrlService = absoluteUrlService;
		    BraintreePageBuilder = braintreePageBuilder;
	    }

	    /// <summary>
        /// Renders the page with the information needed by the payment provider.
        /// </summary>
        /// <remarks>
        /// Output is formatted in accordance to the payment form template configured.
        /// </remarks>
        /// <param name="paymentRequest">The payment request.</param>
        /// <returns>The html rendered.</returns>
        public override string RenderPage(PaymentRequest paymentRequest)
        {
            return BraintreePageBuilder.Build(paymentRequest);
        }

        /// <summary>
        /// Redirect to the configured payment form.
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <returns></returns>
        public override Payment RequestPayment(PaymentRequest paymentRequest)
        {
	        string paymentFormUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentFormUrl;

            if (paymentFormUrl == "(auto)")
                return base.RequestPayment(paymentRequest);

            if (paymentRequest.Payment == null)
                paymentRequest.Payment = CreatePayment(paymentRequest);

            paymentRequest.Payment["paymentGuid"] = Guid.NewGuid().ToString();

            Payment payment = paymentRequest.Payment;

            HttpContext.Current.Response.Redirect(string.Format("{0}?paymentGuid={1}", paymentFormUrl, payment["paymentGuid"]));

            return payment;
        }

        /// <summary>
        /// Processes the callback and excecutes a pipeline if there is one specified for this paymentmethodservice.
        /// </summary>
        /// <param name="payment">The payment to process.</param>
        public override void ProcessCallback(Payment payment)
        {
			string paymentFormUrl = payment.PaymentMethod.DynamicProperty<string>().PaymentFormUrl;
			string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string declineUrl = payment.PaymentMethod.DynamicProperty<string>().DeclineUrl;
			
			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
                return;

            Result<Transaction> result = GetBraintreeGateway(payment.PaymentMethod).TransparentRedirect.ConfirmTransaction(HttpContext.Current.Request.Url.Query);
            if (!result.IsSuccess())
                HttpContext.Current.Response.Redirect(paymentFormUrl == "(auto)"
                    ? string.Format("/{0}/{1}/PaymentRequest.axd?errorMessage={2}", payment.PaymentMethod.PaymentMethodId, payment["paymentGuid"], result.Message.Replace('\n', ';'))
                    : string.Format("{0}?paymentGuid={1}&errorMessage={2}", paymentFormUrl, payment["paymentGuid"], result.Message.Replace('\n', ';')));

            // Validates that the currency configured in the merchant account matches payment currency.
            // We can only check the currency when the transaction is returned from Braintree. There's no way to
            // to verify that the configured currency in Braintree is the same as the billing currency before submitting
            // the transaction.
            Transaction transaction = result.Target;
            if (transaction.CurrencyIsoCode.ToLower() != payment.PurchaseOrder.BillingCurrency.ISOCode.ToLower())
                throw new InvalidOperationException(string.Format("The payment currency ({0}) and the currency configured for the merchant account ({1}) doesn't match. Make sure that the payment currency matches the currency selected in the merchant account.", payment.PurchaseOrder.BillingCurrency.ISOCode.ToUpper(), transaction.CurrencyIsoCode.ToUpper()));

            var paymentStatus = PaymentStatusCode.Declined;
            if (result.IsSuccess())
            {
                if (string.IsNullOrEmpty(transaction.Id))
                    throw new ArgumentException(@"transactionId must be present in query string.");
                if (transaction.Id.Length < 6)
                    throw new ArgumentException(@"transactionId must be at least 6 characters in length.");
                payment.TransactionId = transaction.Id;
                paymentStatus = PaymentStatusCode.Authorized;
            }

            payment.PaymentStatus = PaymentStatus.Get((int)paymentStatus);
            ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
            HttpContext.Current.Response.Redirect(paymentStatus == PaymentStatusCode.Authorized 
				? new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString() 
				: new Uri(_absoluteUrlService.GetAbsoluteUrl(declineUrl)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
        }

        /// <summary>
        /// Acquires the payment from the payment provider.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="status">The status.</param>
        /// <returns>Succes</returns>
        protected override bool AcquirePaymentInternal(Payment payment, out string status)
        {
            //Verify transaction status with braintree.
	        var gateway = GetBraintreeGateway(payment.PaymentMethod);
            Transaction transaction = gateway.Transaction.Find(payment.TransactionId);
            if (transaction.Status == TransactionStatus.SUBMITTED_FOR_SETTLEMENT || transaction.Status == TransactionStatus.SETTLING || transaction.Status == TransactionStatus.SETTLED)
            {
                status = string.Format("The payment is in the process of being acquired. Status is '{0}'. This might be because the order was submitted in the merchant interface.", transaction.Status.ToString().ToLower());
                payment.PaymentStatus = PaymentStatus.Get((int) PaymentStatusCode.Acquired);
                return true;
            }

            Result<Transaction> result = gateway.Transaction.SubmitForSettlement(payment.TransactionId);
            var paymentStatus = PaymentStatusCode.AcquireFailed;
            if (result.IsSuccess())
            {
                paymentStatus = PaymentStatusCode.Acquired;
                status = "";
            }
            else
                status = GetErrorMessage(result);

            payment.PaymentStatus = PaymentStatus.Get((int)paymentStatus);
            return result.IsSuccess();
        }

        /// <summary>
        /// Cancels the payment with the payment gateway.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="status">The status.</param>
        /// <returns>Succes</returns>
        protected override bool CancelPaymentInternal(Payment payment, out string status)
        {
            //Verify transaction status with braintree.
	        var gateway = GetBraintreeGateway(payment.PaymentMethod);
            Transaction transaction = gateway.Transaction.Find(payment.TransactionId);
            if (transaction.Status != TransactionStatus.AUTHORIZED && transaction.Status != TransactionStatus.SUBMITTED_FOR_SETTLEMENT)
                throw new InvalidOperationException(string.Format("Coundn't void payment that doesn't have a status of either 'authorized' or 'submitted_for_settlement'. Payment status is '{0}'.", transaction.Status.ToString().ToLower()));

            Result<Transaction> result = gateway.Transaction.Void(payment.TransactionId);
            if (result.IsSuccess())
            {
                status = "";
                payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Cancelled);
            }
            else
                status = GetErrorMessage(result);

            return result.IsSuccess();
        }

        /// <summary>
        /// Refunds the payment from the payment provider.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="status">The status.</param>
        /// <returns>Succes</returns>
        protected override bool RefundPaymentInternal(Payment payment, out string status)
        {
	        var gateway = GetBraintreeGateway(payment.PaymentMethod);
            Transaction transaction = gateway.Transaction.Find(payment.TransactionId);

            if (transaction.Status == TransactionStatus.AUTHORIZED || transaction.Status == TransactionStatus.SUBMITTED_FOR_SETTLEMENT)
                return CancelPaymentInternal(payment, out status);

            if (transaction.Status != TransactionStatus.SETTLING && transaction.Status != TransactionStatus.SETTLED)
                throw new InvalidOperationException(string.Format("It is not possible to refund a payment with a remote status of either 'settling' or 'settled'. Remote payment status is '{0}'.", transaction.Status.ToString().ToLower()));

            Result<Transaction> result = gateway.Transaction.Refund(payment.TransactionId);
            if (result.IsSuccess())
            {
                status = "";
                payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.Refunded);
            }
            else
                status = GetErrorMessage(result);

            return result.IsSuccess();
        }

        /// <summary>
        /// Gets the error message for the transaction in a appropriate format.
        /// </summary>
        /// <param name="transactionResult">The transaction result</param>
        /// <returns>Error message</returns>
        protected virtual string GetErrorMessage(Result<Transaction> transactionResult)
        {
            var errorString = new StringBuilder();
            errorString.Append(transactionResult.Target.Status.ToString());

            errorString.Append(" - ErrorMessages: ");
            foreach (ValidationError error in transactionResult.Errors.DeepAll())
            {
                errorString.Append(error.Message);
                errorString.Append("; ");
            }
            return errorString.ToString();
        }
    }
}
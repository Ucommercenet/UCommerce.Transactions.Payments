using System;
using System.ServiceModel;
using System.Text;
using UCommerce.EntitiesV2;
using System.Web;
using UCommerce.Extensions;
using UCommerce.Infrastructure;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Infrastructure.Globalization;
using UCommerce.Transactions.Payments.Netaxept.NetaxeptBackendService;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.Netaxept
{
	/// <summary>
	/// Implementation of the Netaxept payment provider
	/// </summary>
	public class NetaxeptPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly ILoggingService _loggingService;
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private readonly ICallbackUrl _callbackUrl;
		private CustomGlobalization LocalizationContext { get; set; }

		protected NetaxeptClient GetClient(PaymentMethod paymentMethod)
		{
			var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
			{
				CloseTimeout = TimeSpan.FromMinutes(1),
				OpenTimeout = TimeSpan.FromMinutes(1),
				ReceiveTimeout = TimeSpan.FromMinutes(10),
				SendTimeout = TimeSpan.FromMinutes(10),
				AllowCookies = false,
				BypassProxyOnLocal = false,
				HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
				MaxBufferSize = 65536,
				MaxBufferPoolSize = 524288,
				MaxReceivedMessageSize = 65536,
				MessageEncoding = WSMessageEncoding.Text,
				TextEncoding = Encoding.UTF8,
				TransferMode = TransferMode.Buffered,
				UseDefaultWebProxy = true
			};

			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			string uri = (testMode) ? TEST_ENDPOINT : LIVE_ENDPOINT;

			var endpointAddress = new EndpointAddress(uri);

			return new NetaxeptClient(binding, endpointAddress);
		}

		private const string TEST_ENDPOINT = "https://epayment-test.bbs.no/netaxept.svc";
		private const string LIVE_ENDPOINT = "https://epayment.bbs.no/netaxept.svc";

		private const string BASE_REDIRECT_URL_TEST = "https://epayment-test.bbs.no/Terminal/default.aspx";
		private const string BASE_REDIRECT_URL_LIVE = "https://epayment.bbs.no/terminal/default.aspx";

		/// <summary>
		/// Initializes a new instance of the <see cref="NetaxeptPaymentMethodService"/> class.
		/// </summary>
		public NetaxeptPaymentMethodService(
			ILoggingService loggingService,
			IAbsoluteUrlService absoluteUrlService,
			ICallbackUrl callbackUrl)
		{
			_loggingService = loggingService;
			_absoluteUrlService = absoluteUrlService;
			_callbackUrl = callbackUrl;
			LocalizationContext = new CustomGlobalization();
		}

		/// <summary>
		/// Validates that the currency matches the provider. Makes placeholder request.
		/// </summary>
		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			Guard.Against.UnsupportedCurrencyInNetaxept(paymentRequest.Payment.PurchaseOrder.BillingCurrency);
			Guard.Against.Null(paymentRequest.Payment.PurchaseOrder.BillingAddress, "PurchaseOrder.BillingAddress must be supplied for Netaxept payments. Please make sure that you update the property prior to initiating payment either by using the API TransactionLibrary.EditBillingInformation() or setting the property directly.");

			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			Payment payment = paymentRequest.Payment;

			var billingAddress = payment.PurchaseOrder.GetBillingAddress();

			string orderCurrency = paymentRequest.Payment.PurchaseOrder.BillingCurrency.ISOCode;
			string referenceId = payment.ReferenceId;
			int orderAmount = Convert.ToInt32(paymentRequest.Amount.Value.ToCents());
			int orderTax = Convert.ToInt32(paymentRequest.PurchaseOrder.VAT.Value.ToCents());

			string customerFirstName = billingAddress.FirstName;
			string customerLastName = billingAddress.LastName;
			string customerEmail = billingAddress.EmailAddress;
			string customerCompany = billingAddress.CompanyName;
			string customerStreet1 = billingAddress.Line1;
			string customerStreet2 = billingAddress.Line2;
			string customerPostalCode = billingAddress.PostalCode;
			string customerCity = billingAddress.City;
			string customerCountry = billingAddress.Country.Name;
			string customerPhone = string.Format("{0}, {1}", billingAddress.PhoneNumber,
												 billingAddress.MobilePhoneNumber);

			// Configuration data
			string merchantId = paymentRequest.PaymentMethod.DynamicProperty<string>().MerchantId;
			string password = paymentRequest.PaymentMethod.DynamicProperty<string>().Password;
			string callbackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string availablePaymentOptions = paymentRequest.PaymentMethod.DynamicProperty<string>().AvailablePaymentOptions;
			bool singlePageView = paymentRequest.PaymentMethod.DynamicProperty<bool>().SinglePageView;
			bool force3DSecure = paymentRequest.PaymentMethod.DynamicProperty<bool>().Force3DSecure;

			// Placeholder request
			NetaxeptClient client = GetClient(paymentRequest.PaymentMethod);
			RegisterResponse registerResponse = client.Register(merchantId, password, new RegisterRequest
			{
				Terminal = new Terminal
				{
					// Where to send the customer when the transaction has been registred at nets
					RedirectUrl = _callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment),
					Language = GetLanguage(),
					PaymentMethodList = availablePaymentOptions,
					SinglePage = singlePageView.ToString(),
					Vat = orderTax.ToString()
				},
				Order = new Order
				{
					Amount = orderAmount.ToString(),
					CurrencyCode = orderCurrency,
					OrderNumber = referenceId,
					Force3DSecure = force3DSecure.ToString()
				},
				Customer = new NetaxeptBackendService.Customer
				{
					FirstName = customerFirstName,
					LastName = customerLastName,
					Email = customerEmail,
					CompanyName = customerCompany,
					Address1 = customerStreet1,
					Address2 = customerStreet2,
					Postcode = customerPostalCode,
					Town = customerCity,
					Country = customerCountry,
					PhoneNumber = customerPhone
				},
				Environment = new NetaxeptBackendService.Environment
				{
					WebServicePlatform = "WCF"
				}
			});

			RedirectCustomerToRemotePaymentPage(registerResponse.TransactionId, paymentRequest.PaymentMethod);

			return payment;
		}

		/// <summary>
		/// Processed the callback received from the payment provider.
		/// </summary>
		public override void ProcessCallback(Payment payment)
		{
			if (payment.PaymentStatus.PaymentStatusId != (int)PaymentStatusCode.PendingAuthorization)
				return;

			string transactionId = HttpContext.Current.Request["transactionId"];
			string paymentResponseCode = HttpContext.Current.Request["responseCode"];

			// Configuration values
			string cancelUrl = payment.PaymentMethod.DynamicProperty<string>().CancelUrl;
			string merchantId = payment.PaymentMethod.DynamicProperty<string>().MerchantId;
			string password = payment.PaymentMethod.DynamicProperty<string>().Password;
			string declineUrl = payment.PaymentMethod.DynamicProperty<string>().DeclineUrl;

			// Netaxept will return something other than OK if
			// the customer cancels at the remote end.
			if (!paymentResponseCode.Equals("OK"))
			{
				HttpContext.Current.Response.Redirect(new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl))
					.AddOrderGuidParameter(payment.PurchaseOrder).ToString());
			}

			bool authOK = false;
			NetaxeptClient client = GetClient(payment.PaymentMethod);

			try
			{
				//Authorization process
				var authResponse = client.Process(merchantId, password, new ProcessRequest
					{
						Operation = "AUTH",
						TransactionId = transactionId
					});

				// PaymentClient.Process will cause an exception if something
				// goes wrong during auth. Thus authReponse will always
				// contain an "OK" value if execution continues to this
				// point.
				authOK = authResponse.ResponseCode == "OK";
				
				if (authOK)
					UpdatePaymentAndDisplayThankYouPage(payment, transactionId);
			}
			catch (Exception ex)
			{
				_loggingService.Log<NetaxeptPaymentMethodService>(ex.Message);

				string uri = new Uri(_absoluteUrlService.GetAbsoluteUrl(declineUrl))
					.AddOrderGuidParameter(payment.PurchaseOrder)
					.AddQueryStringParameter("exceptionMessage", ex.Message)
					.ToString();

				HttpContext.Current.Response.Redirect(uri, false);
			}
		}

		/// <summary>
		/// Cancels the payment from the payment provider. This is often used when you need to call external services to handle the cancel process.
		/// </summary>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			string message;
			bool result = ProcessOperation("ANNUL", payment.TransactionId, out message, payment.PaymentMethod);
			status = result ? PaymentMessages.CancelSuccess : PaymentMessages.CancelFailed;
			status = ConcatMessage(status, message);

			return result;
		}

		/// <summary>
		/// Acquires the payment from the payment provider. This is often used when you need to call external services to handle the acquire process.
		/// </summary>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			string message;
			bool result = ProcessOperation("CAPTURE", payment.TransactionId, out message, payment.PaymentMethod);

			status = result ? PaymentMessages.AcquireSuccess : PaymentMessages.AcquireFailed;
			status = ConcatMessage(status, message);

			return result;
		}

		/// <summary>
		/// Refunds the payment from the payment provider. This is often used when you need to call external services to handle the refund process.
		/// </summary>
		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			string message;
			bool result = ProcessOperation("CREDIT", payment.TransactionId, out message, payment.PaymentMethod);
			status = result ? PaymentMessages.RefundSuccess : PaymentMessages.RefundFailed;
			status = ConcatMessage(status, message);

			return result;
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotImplementedException("Netaxept does not need a local form. Use RequestPayment instead.");
		}

		protected virtual string GetLanguage()
		{
			string languageCode = "en_GB";
			switch (LocalizationContext.CurrentCultureCode)
			{
				case "da":
				case "da-DK":
					languageCode = "da_DK";
					break;

				case "sv":
				case "sv-SE":
					languageCode = "sv_SE";
					break;

				case "no":
				case "nb-NO":
				case "nn-NO":
					languageCode = "no_NO";
					break;

				case "de":
				case "de-DE":
					languageCode = "de_DE";
					break;
			}

			return languageCode;
		}

		protected virtual bool ProcessOperation(string operation, string transactionId, out string message, PaymentMethod paymentMethod)
		{
			// Configuration values
			string merchantId = paymentMethod.DynamicProperty<string>().MerchantId;
			string password = paymentMethod.DynamicProperty<string>().Password;

			bool result;

			NetaxeptClient client = GetClient(paymentMethod);

			try
			{
				var response = client.Process(merchantId, password, new ProcessRequest
				{
					Operation = operation,
					TransactionId = transactionId
				});

				message = response.ResponseText;
				result = response.ResponseCode.Equals("OK");
			}
			catch (Exception ex)
			{
				message = ex.Message;
				result = false;
			}

			return result;
		}

		private void UpdatePaymentAndDisplayThankYouPage(Payment payment, string transactionId)
		{
			// Configuration value
			string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;

			var paymentStatus = (int)PaymentStatusCode.Authorized;
			payment.PaymentStatus = PaymentStatus.Get(paymentStatus);
			payment.TransactionId = transactionId;

			ExecutePostProcessingPipeline(payment);

			HttpContext.Current.Response.Redirect(
				new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl))
					.AddOrderGuidParameter(payment.PurchaseOrder).ToString(), false);
		}

		private void RedirectCustomerToRemotePaymentPage(string transactionId, PaymentMethod paymentMethod)
		{
			// Configuration value
			string merchantId = paymentMethod.DynamicProperty<string>().MerchantId;
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;

			string idData = string.Format("?merchantId={0}&transactionId={1}", merchantId, transactionId);
			string redirectUrl = (testMode) ? string.Format("{0}{1}", BASE_REDIRECT_URL_TEST, idData) : string.Format("{0}{1}", BASE_REDIRECT_URL_LIVE, idData);

            HttpContext.Current.Response.Redirect(redirectUrl);
		}

		private string ConcatMessage(string status, string message)
		{
			if (!string.IsNullOrWhiteSpace(message))
				status += string.Format(" >> {0}.", message);

			return status;
		}
	}
}
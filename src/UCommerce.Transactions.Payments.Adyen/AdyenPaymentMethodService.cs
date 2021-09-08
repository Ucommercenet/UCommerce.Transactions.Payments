﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.ServiceModel;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Transactions.Payments.Adyen.Adyen.Test.ModificationSoapService;
using Ucommerce.Transactions.Payments.Adyen.Adyen.Test.RecurringSoapService;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;
using Amount = Ucommerce.Transactions.Payments.Adyen.Adyen.Test.ModificationSoapService.Amount;
using Recurring = Ucommerce.Transactions.Payments.Adyen.Adyen.Test.ModificationSoapService.Recurring;

namespace Ucommerce.Transactions.Payments.Adyen
{
	public class AdyenPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly IAbsoluteUrlService _absoluteUrlService;
		protected ILoggingService LoggingService { get; set; }
		protected AdyenPageBuilder PageBuilder { get; set; }
		protected IHttpPaymentExtractor HttpPaymentExtractor { get; set; }
		protected PaymentPortTypeClient PaymentClient { get; set; }
		protected RecurringPortTypeClient RecurringClient { get; set; }
		protected PaymentResultValidator ResultValidator { get; set; }

		private string GetModificationServiceUrl(PaymentMethod paymentMethod)
		{
			if (paymentMethod.DynamicProperty<bool>().Live)
			{
				return "https://pal-live.adyen.com/pal/servlet/soap/Payment";
			}
			return "https://pal-test.adyen.com/pal/servlet/soap/Payment";
		}

		private string GetRecurringServiceUrl(PaymentMethod paymentMethod)
		{
			if (paymentMethod.DynamicProperty<bool>().Live)
			{
				return "https://pal-live.adyen.com/pal/servlet/soap/Recurring";
			}
			return "https://pal-test.adyen.com/pal/servlet/soap/Recurring";
		}

		private const string RecurringDetailReference = "RecurringDetailReference";
		private const string LatestPspReference = "LatestPspReference";

		public AdyenPaymentMethodService(ILoggingService loggingService,
			AdyenPageBuilder pageBuilder,
			IHttpPaymentExtractor httpExtractor,
			IAbsoluteUrlService absoluteUrlService)
		{
			_absoluteUrlService = absoluteUrlService;
			LoggingService = loggingService;
			PageBuilder = pageBuilder;
			HttpPaymentExtractor = httpExtractor;
			ResultValidator = new PaymentResultValidator(loggingService);
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			return PageBuilder.Build(paymentRequest);
		}

		public override Payment CreatePayment(PaymentRequest request)
		{
			var payment = base.CreatePayment(request);
			payment.ReferenceId = GetReferenceId(request);
			payment.PaymentStatus = PaymentStatus.Get((int)PaymentStatusCode.PendingAuthorization);
			payment.TransactionId = null;
			payment["paymentGuid"] = Guid.NewGuid().ToString();
			payment[LatestPspReference] = "EMPTY";

			request.PurchaseOrder.AddPayment(payment);

			payment.Save();
			return payment;
		}

		public override void ProcessCallback(Payment payment)
		{
			var request = HttpContext.Current.Request;

			var dict = BuildDictionaryOfParameters(request);

			if (RequestIsAuthorizationReponse(dict))
			{
				ProcessAuthorizarionResponse(payment, dict);
			}
			else
			{
				ProcessNotificationMessage(payment, dict);
			}
		}

		private void ProcessAuthorizarionResponse(Payment payment, Dictionary<string, string> dict)
		{
			bool authorizedOrPending = ProcessAuthorizationResultMessage(payment, dict);

			if (authorizedOrPending)
			{
				HttpContext.Current.Response.Redirect(
					new Uri(_absoluteUrlService.GetAbsoluteUrl(payment.PaymentMethod.DynamicProperty<string>().AcceptUrl))
						.AddOrderGuidParameter(
							payment.PurchaseOrder).ToString());
			}
			else
			{
				HttpContext.Current.Response.Redirect(
					new Uri(_absoluteUrlService.GetAbsoluteUrl(payment.PaymentMethod.DynamicProperty<string>().DeclineUrl))
						.AddOrderGuidParameter(
							payment.PurchaseOrder).ToString());
			}
		}

		private void ProcessNotificationMessage(Payment payment, Dictionary<string, string> dict)
		{
			SendNotificationReceivedMessage();

			bool emptyPayment;
			bool.TryParse(payment["EmptyPayment"], out emptyPayment);

			if (emptyPayment)
			{
				//General notification recieved from Adyen. 
				LoggingService.Debug<AdyenPaymentMethodService>(
					string.Format("Notification received, but no payment was found with Reference ID: " + payment["ReferenceId"]));
			}
			else
			{
				//Payment specific notification for example an auth message.
				ProcessPaymentNotificationMessage(payment, dict);
			}
		}

		private bool RequestIsAuthorizationReponse(Dictionary<string, string> dict)
		{
			return dict.ContainsKey("authResult");
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			try
			{
				SetupWebServiceClients(payment.PaymentMethod);
				SetupWebServiceCredentials(payment.PaymentMethod);
				var result = PaymentClient.cancel(new ModificationRequest
				{
					merchantAccount = payment.PaymentMethod.DynamicProperty<string>().MerchantAccount,
					originalReference = payment.TransactionId
				});

				if (result.response == "[cancel-received]")
				{
					payment[LatestPspReference] = result.pspReference;
					payment.Save();
					status = PaymentMessages.CancelPending;
					return true;
				}

				status = PaymentMessages.CancelFailed + ": " + result.response;
				return false;
			}
			catch (Exception ex)
			{
				LoggingService.Error<AdyenPaymentMethodService>(ex, "Caught trying to Cancel a payment.");
				status = PaymentMessages.AcquireFailed + ": " + ex.Message;
				return false;
			}
		}

		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			try
			{
				if (payment.PaymentMethod.DynamicProperty<bool>().UseRecurringContract)
				{
					string authStatus;
					AuthorizeRecurringPaymentInternal(payment, out authStatus);
				}
				return AcquireReqularPaymentInternal(payment, out status);
			}
			catch (CommunicationException ex)
			{
				LoggingService.Error<AdyenPaymentMethodService>(ex, "Caught trying to Acquire a payment.");
				status = PaymentMessages.AcquireFailed + ": " + ex.Message;
				return false;
			}
		}

		private void AuthorizeRecurringPaymentInternal(Payment payment, out string status)
		{
			var originalPayment = payment.PurchaseOrder.Payments.FirstOrDefault(x =>
				x.PaymentProperties.Any(y => y.Key == RecurringDetailReference));

			if (originalPayment == null)
			{
				throw new SecurityException("There was no payment found, with recurring details reference attached. Please make sure the recurring payment was authorized correctly.");
			}

			SetupWebServiceClients(payment.PaymentMethod);
			SetupWebServiceCredentials(payment.PaymentMethod);
			var result = PaymentClient.authorise(
				new Adyen.Test.ModificationSoapService.PaymentRequest
				{
					selectedRecurringDetailReference = originalPayment[RecurringDetailReference],
					recurring = new Recurring
					{
						contract = "RECURRING"
					},
					merchantAccount = payment.PaymentMethod.DynamicProperty<string>().MerchantAccount,
					amount = new Amount
					{
						value = payment.Amount.ToCents(),
						currency = payment.PurchaseOrder.BillingCurrency.ISOCode
					},
					reference = payment.PaymentId.ToString(CultureInfo.InvariantCulture),
					shopperEmail = payment.PurchaseOrder.BillingAddress.EmailAddress,
					shopperReference = originalPayment.ReferenceId,
					shopperInteraction = "ContAuth",
					fraudOffset = payment.PaymentMethod.DynamicProperty<int>().Offset
				});

			payment.TransactionId = result.pspReference;

			if (result.resultCode == "Authorised")
			{
				SetPaymentStatus(payment, PaymentStatusCode.Acquired, "Recurring payment authorized.");
				status = "Recurring payment authorized.";
				return;
			}

			if (result.resultCode == "Refused")
			{
				SetPaymentStatus(payment, PaymentStatusCode.Declined,
								 "Recurring payment could not be authorized: " + result.refusalReason);
				status = "Recurring payment could not be authorized: " + result.refusalReason;
				return;
			}

			SetPaymentStatus(payment, PaymentStatusCode.Declined,
								"Error occured during authorization of recurring payment: " + result.refusalReason);
			status = "Error occured during authorization of recurring payment: " + result.refusalReason;
		}

		private bool AcquireReqularPaymentInternal(Payment payment, out string status)
		{
			SetupWebServiceClients(payment.PaymentMethod);
			SetupWebServiceCredentials(payment.PaymentMethod);

			var result = PaymentClient.capture(new ModificationRequest
			{
				merchantAccount = payment.PaymentMethod.DynamicProperty<string>().MerchantAccount,
				originalReference = payment.TransactionId,
				modificationAmount = new Amount
				{
					value = payment.Amount.ToCents(),
					currency = payment.PurchaseOrder.BillingCurrency.ISOCode
				}
			});

			if (result.response == "[capture-received]")
			{
				payment[LatestPspReference] = result.pspReference;
				payment.Save();
				status = PaymentMessages.AcquirePending;
				return true;
			}

			status = PaymentMessages.AcquireFailed + ": " + result.response;
			return false;
		}

		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			try
			{
				SetupWebServiceClients(payment.PaymentMethod);
				SetupWebServiceCredentials(payment.PaymentMethod);

				var result = PaymentClient.cancelOrRefund(new ModificationRequest
				{
					merchantAccount = payment.PaymentMethod.DynamicProperty<string>().MerchantAccount,
					originalReference = payment.TransactionId
				});

				if (result.response == "[cancelOrRefund-received]")
				{
					payment[LatestPspReference] = result.pspReference;
					payment.Save();
					status = PaymentMessages.RefundPending;
					return true;
				}

				status = PaymentMessages.RefundFailed + ": " + result.response;
				return false;
			}
			catch (Exception ex)
			{
				LoggingService.Error<AdyenPaymentMethodService>(ex, "Caught trying to Refund a payment.");
				status = PaymentMessages.RefundFailed + ": " + ex.Message;
				return false;
			}
		}

		/// <summary>
		/// Extracts payment from request using the default payment gateway callback extractor.
		/// </summary>
		/// <param name="httpRequest"></param>
		/// <returns></returns>
		public override Payment Extract(HttpRequest httpRequest)
		{
			return HttpPaymentExtractor.Extract(httpRequest);
		}

		private void SetupWebServiceClients(PaymentMethod paymentMethod)
		{
			var binding = new BasicHttpBinding
			{
				Security = new BasicHttpSecurity
				{
					Mode = BasicHttpSecurityMode.Transport,
					Message = new BasicHttpMessageSecurity
					{
						ClientCredentialType = BasicHttpMessageCredentialType.UserName
					},
					Transport = new HttpTransportSecurity
					{
						ClientCredentialType = HttpClientCredentialType.Basic,
					}
				}
			};

			var paymentUri = new Uri(GetModificationServiceUrl(paymentMethod), UriKind.Absolute);
			var recurringUri = new Uri(GetRecurringServiceUrl(paymentMethod), UriKind.Absolute);

			PaymentClient = new PaymentPortTypeClient(binding, new EndpointAddress(paymentUri));
			RecurringClient = new RecurringPortTypeClient(binding, new EndpointAddress(recurringUri));
		}

		private void SetupWebServiceCredentials(PaymentMethod paymentMethod)
		{
			if (PaymentClient.ClientCredentials == null || RecurringClient.ClientCredentials == null)
			{
				throw new InvalidOperationException("Could not set user credentials for the web service access.");
			}

			PaymentClient.ClientCredentials.UserName.UserName = paymentMethod.DynamicProperty<string>().WebServiceUsername;
			PaymentClient.ClientCredentials.UserName.Password = HttpUtility.UrlDecode(paymentMethod.DynamicProperty<string>().WebServicePassword);

			RecurringClient.ClientCredentials.UserName.UserName = paymentMethod.DynamicProperty<string>().WebServiceUsername;
			RecurringClient.ClientCredentials.UserName.Password = HttpUtility.UrlDecode(paymentMethod.DynamicProperty<string>().WebServicePassword);
		}

		private NotificationMessageData RetrieveNotificationMessageData(IDictionary<string, string> dict)
		{
			var data = new NotificationMessageData();
			data.ExtractDataFromRequest(dict);
			return data;
		}

		private AuthorizationResultMessageData RetrieveAuthenticationResultMessageData(IDictionary<string, string> dict)
		{
			var data = new AuthorizationResultMessageData();
			data.ExtractDataFromRequest(dict);
			return data;
		}

		private Dictionary<string, string> BuildDictionaryOfParameters(HttpRequest request)
		{
			var dict = new Dictionary<string, string>();

			foreach (var key in request.QueryString.AllKeys)
			{
				dict[key] = request.QueryString[key];
				LoggingService.Debug<AdyenPaymentMethodService>(string.Format("Querystring Parameter '{0}'='{1}'", key, request[key]));
			}

			foreach (var key in request.Form.AllKeys)
			{
				dict[key] = request.Form[key];
				LoggingService.Debug<AdyenPaymentMethodService>(string.Format("Form Parameter '{0}'='{1}'", key, request[key]));
			}

			return dict;
		}

		private bool ProcessAuthorizationResultMessage(Payment payment, Dictionary<string, string> dict)
		{
			if (payment.PaymentStatus.PaymentStatusId == (int)PaymentStatusCode.Cancelled) { return false; }

			//Check that signature is valid to prevent fraud.
			Guard.Against.MessageNotAuthenticated(ResultValidator.ValidateSignature(dict, payment.PaymentMethod));

			var data = RetrieveAuthenticationResultMessageData(dict);
			payment.TransactionId = data.PspReference;
			payment[LatestPspReference] = data.PspReference;

			var authenticatedOrPending = false;
			switch (data.AuthorizationResult)
			{
				case AuthorizationResult.Authorised:
					SetPaymentStatus(payment, PaymentStatusCode.Authorized, "Payment authorized.");

					if (!payment.PaymentMethod.DynamicProperty<bool>().UseRecurringContract)
					{
						if (!CheckoutPipelineHasAlreadyBeenExecutedForPayment(payment)) //The checkout pipeline may already have been run at this point.
						{
							payment.PurchaseOrder.OrderStatus = OrderStatus.Get((int)OrderStatusCode.Processing); //to remove the link from the basket.
							payment.PurchaseOrder.Save();	
						}
					}

					// Ok to run the checkout pipeline already now, since we know the payment is authorized.
					//ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

					if (payment.PaymentMethod.DynamicProperty<bool>().UseRecurringContract)
					{
						CancelAuthorizationBeforeCapture(payment);
						SaveRecurringDetailReference(payment);
					}

					authenticatedOrPending = true;
					break;
				case AuthorizationResult.Cancelled:
					SetPaymentStatus(payment, PaymentStatusCode.Cancelled, "Payment cancelled.");
					break;
				case AuthorizationResult.Pending:
					if (!CheckoutPipelineHasAlreadyBeenExecutedForPayment(payment))
						//The checkout pipeline may already have been run at this point.
					{
						SetPaymentStatus(payment, PaymentStatusCode.PendingAuthorization, "Authorization pending.");
						authenticatedOrPending = true;

						// Set order in processing state.
						payment.PurchaseOrder.OrderStatus = OrderStatus.Get((int)OrderStatusCode.Processing);
						payment.PurchaseOrder.Save();
					}
					break;
				case AuthorizationResult.Error:
					SetPaymentStatus(payment, PaymentStatusCode.Declined, "Error during authorization.");
					break;
				case AuthorizationResult.Refused:
					SetPaymentStatus(payment, PaymentStatusCode.AcquireFailed, "Authorization refused.");
					break;
				default:
					SetPaymentStatus(payment, PaymentStatusCode.Declined, string.Format("Warning: {0} is not a supported authentication result", data.AuthorizationResult));
					break;
			}

			return authenticatedOrPending;
		}

		protected void CancelAuthorizationBeforeCapture(Payment payment)
		{
			string status;
			if (CancelPaymentInternal(payment, out status))
			{
				// TODO: What should the payment status be? A new status? "RecurringStarted"?
				SetPaymentStatus(payment, PaymentStatusCode.Cancelled,
								 "Cancelling the initial payment used for authorizing a recurring payment.");
			}
			else
			{
				// TODO: What should the payment status be? A new status?
				SetPaymentStatus(payment, PaymentStatusCode.Declined,
								 "Cancelling the initial authorization payment failed with message: " + status);
			}
		}

		protected void SaveRecurringDetailReference(Payment payment)
		{
			try
			{
				var result = RecurringClient.listRecurringDetails(new RecurringDetailsRequest
				{
					shopperReference = payment.ReferenceId,
					recurring = new Adyen.Test.RecurringSoapService.Recurring
					{
						contract = "RECURRING"
					},
					merchantAccount = payment.PaymentMethod.DynamicProperty<string>().MerchantAccount
				});

				var latestDetail = result.details.OrderByDescending(x => x.creationDate).FirstOrDefault();

				if (latestDetail != null)
				{
					payment[RecurringDetailReference] = latestDetail.recurringDetailReference;
					payment.Save();
				}
				else
				{
					throw new SecurityException("Could not retrive the recurring detail reference. Please make sure that recurring payments are activated for your account.");
				}
			}
			catch (Exception ex)
			{
				LoggingService.Error<AdyenPaymentMethodService>(
					ex,
					string.Format("Saving recurring detail reference failed for payment with reference id: {0}, exception: {1}", payment.ReferenceId, ex.Message));
			}
		}

		protected void ProcessPaymentNotificationMessage(Payment payment, Dictionary<string, string> dict)
		{
			var data = RetrieveNotificationMessageData(dict);
			payment[LatestPspReference] = data.PspReference;

            Guard.Against.MessageNotAuthenticated(ResultValidator.NotificationMessageIsAuthenticated(payment.PaymentMethod));

            switch (data.EventCode)
			{
				case PaymentEvent.Authorization:
					if (data.Success)
					{
						if (payment.PaymentMethod.DynamicProperty<bool>().UseRecurringContract) //implementation details for recurring that auth should be canceled.
							CancelAuthorizationBeforeCapture(payment);

						SetPaymentStatus(payment, PaymentStatusCode.Authorized, "Payment authorized. " + data.Reason);
						RunCheckoutPipelineIfNeeded(payment, dict);
					}
					else
						SetPaymentStatus(payment, PaymentStatusCode.Declined, "Payment could not be authorized: " + data.Reason);
					break;
				case PaymentEvent.Capture:
					if (data.Success)
					{
						SetPaymentStatus(payment, PaymentStatusCode.Acquired, "Payment acquired. " + data.Reason);
						RunCheckoutPipelineIfNeeded(payment, dict); 						
					}
					else
						SetPaymentStatus(payment, PaymentStatusCode.AcquireFailed, "Payment could not be acquired: " + data.Reason);
					break;
				case PaymentEvent.CaptureFailed:
					SetPaymentStatus(payment, PaymentStatusCode.AcquireFailed, "Payment could not be acquired: " + data.Reason);
					break;
				case PaymentEvent.Refund:
					if (data.Success)
						SetPaymentStatus(payment, PaymentStatusCode.Refunded, "Payment was refunded. " + data.Reason);
					else
						// TODO: RefundedFailed status needed?
						SetPaymentStatus(payment, PaymentStatusCode.Refunded, "Payment could not be refunded. " + data.Reason);
					break;
				default:
					payment.PurchaseOrder.AddOrderStatusAudit(new OrderStatusAudit { Message = string.Format("Notification received. Event: {0}. Reason: {1}", data.EventCode, data.Reason) });
					break;
			}
		}

		protected virtual bool CheckoutPipelineHasAlreadyBeenExecutedForPayment(Payment payment)
		{
			if (payment.PurchaseOrder.OrderStatus.OrderStatusId == (int) OrderStatusCode.Basket ||
			    payment.PurchaseOrder.OrderStatus.OrderStatusId == (int) OrderStatusCode.Processing)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Run the checkout pipeline for auth or acquire requests if needed.
		/// </summary>
		/// <param name="payment"></param>
		/// <param name="dict"></param>
		protected void RunCheckoutPipelineIfNeeded(Payment payment, Dictionary<string, string> dict)
		{
			if (!CheckoutPipelineHasAlreadyBeenExecutedForPayment(payment))
			{
				ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));
			}
		}

		private void SendNotificationReceivedMessage()
		{
			HttpContext.Current.Response.Write("[accepted]");
		}
	}
}

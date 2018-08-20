using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public class GlobalCollectPaymentMethodService : ExternalPaymentMethodService
	{
		private readonly ILoggingService _loggingService;
		private readonly IGlobalCollectService _globalCollectService;
		private readonly IHttpPaymentExtractor _paymentExtractor;
		private readonly IRepository<PaymentStatus> _paymentStatusRepository;
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private readonly IOrderService _orderService;

		public GlobalCollectPaymentMethodService(
			ILoggingService loggingService,
			IGlobalCollectService globalCollectService,
			IHttpPaymentExtractor paymentExtractor,
			IRepository<PaymentStatus> paymentStatusRepository,
			IAbsoluteUrlService absoluteUrlService,
			IOrderService orderService)
		{
			_loggingService = loggingService;
			_globalCollectService = globalCollectService;
			_paymentExtractor = paymentExtractor;
			_paymentStatusRepository = paymentStatusRepository;
			_absoluteUrlService = absoluteUrlService;
			_orderService = orderService;
		}

		public override string RenderPage(PaymentRequest paymentRequest)
		{
			throw new NotImplementedException("GlobalCollectPaymentMethodService does not need to submit a form on behalf of the customer. Rather a direct to the hosted merchant link should occur prior to RenderForm logic being executed.");
		}

		public override void ProcessCallback(Payment payment)
		{
			var orderStatus = GetOrderStatusFromPayment(payment);
			int paymentStatusId = orderStatus.StatusId;
			// According to WebCollect_technical_Guide_2014_Q1.pdf page 32 a payment is
			// considered successful if status is above 800 and paymentproductid != 11.
			// Looking at the reference list on page 338 it seems that 800 is the only OK
			// status as 1800 for example is refunded and would thus not be considered 
			// authorized.
			//			PaymentStatusCode code = 
			//				paymentStatus == 800 
			//				&& payment["paymentproductid"] != "11" ? PaymentStatusCode.Authorized : PaymentStatusCode.Declined;


			// According to WebCollect_technical_Guide_2014_Q1.pdf page 305 on SET_PAYMENT (acquire) a payment with status 600 is Authorized - special for online credit cards:
			//QUOTE:
			//THIS API (SET_PAYMENT): Settles payments with an additional status for online credit cards of 600: Authorized, waiting for explicit instructions for settlement
			//We should then either SET the payment (SET_PAYMENT api call) if configuration is instantCapture or wait untill the acquire payment is called.

			var paymentMethod = payment.PaymentMethod;

			int code;
			string responseUrl = paymentMethod.DynamicProperty<string>().DeclineUrl;

			//If the payment is not pending as expected the auth is failed.
			if (paymentStatusId != (int)GlobalCollectPaymentStatus.Pending)
			{
				code = (int)PaymentStatusCode.Declined;
				payment.PaymentStatus = _paymentStatusRepository.Get((int)code);
				payment.Save();

				AddAuditMessageToPurchaseOrder(payment, orderStatus);

				RedirectToUrl(responseUrl, payment);
			}

			if (paymentMethod.DynamicProperty<bool>().InstantCapture)
			{
				string status;
				AcquirePaymentInternal(payment, out status);
				code = (int)PaymentStatusCode.Acquired;
			}
			else
			{
				code = (int)PaymentStatusCode.Authorized;
			}

			responseUrl = paymentMethod.DynamicProperty().AcceptUrl;

			payment.PaymentStatus = _paymentStatusRepository.Get((int)code);
			ProcessPaymentRequest(new PaymentRequest(payment.PurchaseOrder, payment));

			RedirectToUrl(responseUrl, payment);
		}

		private void AddAuditMessageToPurchaseOrder(Payment payment, IOrderStatus orderStatus)
		{
			var auditMessage = BuildAuditMessageForDeclinedPaymentRequest(orderStatus);
			_orderService.AddAuditTrail(payment.PurchaseOrder, auditMessage);
			payment.PurchaseOrder.Save();
		}

		private string BuildAuditMessageForDeclinedPaymentRequest(IOrderStatus orderStatus)
		{
			string message;
			var convertedPaymentStatus = PaymentStatusHelper.ConvertPaymentStatusCodeToHumanReadableMessage(orderStatus.StatusId);
			var error = orderStatus.Errors.FirstOrDefault();
			if (error != null)
			{
				message = string.Format("Payment declined. Status code: {0} - {1}. Error type: {2}. Error code: {3}. Error message: {4}", orderStatus.StatusId,
					convertedPaymentStatus, error.Type, error.Code, error.Message);
			}
			else
			{
				message = string.Format("Status code: {0} - {1}.", orderStatus.StatusId, convertedPaymentStatus);
			}
			return message;
		}

		private void RedirectToUrl(string url, Payment payment)
		{
			HttpContext.Current.Response.Redirect(
				new Uri(_absoluteUrlService.GetAbsoluteUrl(url)).AddOrderGuidParameter(payment.PurchaseOrder).ToString());
		}

		/// <summary>
		/// Calls Global Collect for a payment status corresponding to values in GlobalCollectPaymentStatus
		/// </summary>
		/// <param name="payment"></param>
		/// <returns></returns>
		private IOrderStatus GetOrderStatusFromPayment(Payment payment)
		{
			long orderId = TryGetOrderIdFromPayment(payment);
			IOrderStatus statusData = _globalCollectService.GetOrderStatus(payment.PaymentMethod, orderId);
			return statusData;
		}

		public override Payment RequestPayment(PaymentRequest paymentRequest)
		{
			IPaymentData paymentData = null;
			if (paymentRequest.Payment == null)
				paymentRequest.Payment = CreatePayment(paymentRequest);

			try
			{
				paymentData = CallInsertOrderWithPayment(paymentRequest);
			}
			// catch (PaymentAmountOutOfRangeExeption)
			catch (GlobalCollectException exception) //We already logged the error
			{
				// Catch all the exceptions. Not just the PaymentAmountOutOfRangeException.
				// It should never fail by exception. Always decline.
				string status;
				HandleGlobalCollectException(paymentRequest.Payment, exception, out status);
				SetPaymentStatus(paymentRequest.Payment, PaymentStatusCode.Declined, status);

				string declineUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().DeclineUrl;

				RedirectToUrl(declineUrl, paymentRequest.Payment);
			}

		    var redirectUrl = paymentData.FormAction;

            HttpContext.Current.Response.Redirect(redirectUrl);

            return paymentRequest.Payment;
		}

		public override Payment CancelPayment(Payment paymentToCancel)
		{
			var paymentStatus = (PaymentStatusCode)paymentToCancel.PaymentStatus.PaymentStatusId;
			bool statusCode = false;
			var status = string.Empty;

			var globalCollectPaymentStatus = GetOrderStatusFromPayment(paymentToCancel).StatusId;

			if (!IsCancellable(paymentToCancel, globalCollectPaymentStatus)) return paymentToCancel;

			if (PaymentQualifiesForCancel(globalCollectPaymentStatus, paymentToCancel))
				statusCode = CancelPaymentInternal(paymentToCancel, out status);

			if (PaymentQualifiesForRefund(globalCollectPaymentStatus, paymentToCancel))
				statusCode = RefundPaymentInternal(paymentToCancel, out status);

			if (PaymentIsAlreadyCancelled(globalCollectPaymentStatus))
			{
				statusCode = true;
				status = "Payment was already cancelled at Global Collect";
			}

			if (PaymentIsAlreadyRefunded(globalCollectPaymentStatus))
			{
				statusCode = true;
				status = "Payment was already refunded at Global Collect";
			}

			PaymentStatusCode nextStatus = GetNextStatus(statusCode, paymentStatus);

			SetPaymentStatus(paymentToCancel, nextStatus, status);
			return paymentToCancel;
		}

		private bool IsCancellable(Payment paymentToCancel, int globalCollectPaymentStatus)
		{
			const int paypalPaymentProductId = 840;
			if (globalCollectPaymentStatus != (int) GlobalCollectPaymentStatus.Ready)
			{
				return true;
			}

			if (TryGetPaymentProductIdFromPayment(paymentToCancel) != paypalPaymentProductId)
			{
				return true;
			}

			_orderService.AddAuditTrail(paymentToCancel.PurchaseOrder, "It's not possible to refund a payment which status is aquired, when using PayPal");
			return false;
		}

		/// <summary>
		/// Determines weather a payment is fit for refund. 
		/// </summary>
		/// <remarks>
		/// See "AuthFlowDescribedFromSupport.pdf" in docs\IPaymentWindow\Global Collect
		/// </remarks>
		private bool PaymentQualifiesForRefund(int globalCollectPaymentStatus, Payment paymentToCancel)
		{
			switch (globalCollectPaymentStatus)
			{
				case (int)GlobalCollectPaymentStatus.ProcessedOrSent:		//900
				case (int)GlobalCollectPaymentStatus.InvoiceSent:			//950
				case (int)GlobalCollectPaymentStatus.SettlementInProgress:	//975
				case (int)GlobalCollectPaymentStatus.Paid:					//1000
				case (int)GlobalCollectPaymentStatus.AccountDebitted:		//1010
				case (int)GlobalCollectPaymentStatus.Corrected:				//1020
				case (int)GlobalCollectPaymentStatus.WithdrawnChargeback:	//1030
				case (int)GlobalCollectPaymentStatus.Collected:				//1050

					return true;
			}
			return false;
		}

		/// <summary>
		///	Determines weather a payment is fit for cancel. See "AuthFlowDescribedFromSupport.pdf" in docs\IPaymentWindow\Global Collect
		/// </summary>
		/// <remarks>
		/// See "AuthFlowDescribedFromSupport.pdf" in docs\IPaymentWindow\Global Collect
		/// </remarks>
		private bool PaymentQualifiesForCancel(int globalCollectPaymentStatus, Payment paymentToCancel)
		{
			switch (globalCollectPaymentStatus)
			{
				case (int)GlobalCollectPaymentStatus.Pending:				//600
				case (int)GlobalCollectPaymentStatus.AuthorizedAndPending:	//625
				case (int)GlobalCollectPaymentStatus.PendingVerification:	//650
				case (int)GlobalCollectPaymentStatus.Ready:					//800
					return true;
			}
			return false;
		}

		private bool PaymentIsAlreadyCancelled(int globalCollectPaymentStatus)
		{
			return (int)GlobalCollectPaymentStatus.Cancelled == globalCollectPaymentStatus;
		}

		private bool PaymentIsAlreadyRefunded(int globalCollectPaymentStatus)
		{
			return (int)GlobalCollectPaymentStatus.Refunded == globalCollectPaymentStatus;
		}

		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			var orderId = TryGetOrderIdFromPayment(payment);
			var merchantReference = GetMerchantReference(payment);

			try
			{
				_globalCollectService.CancelPayment(payment.PaymentMethod, orderId, merchantReference);
				status = PaymentMessages.CancelSuccess;
				return true;
			}
			catch (GlobalCollectException gce)
			{
				HandleGlobalCollectException(payment, gce, out status);
				return false;
			}
		}

		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			var orderId = TryGetOrderIdFromPayment(payment);
			var paymentProductId = TryGetPaymentProductIdFromPayment(payment);

			try
			{
				_globalCollectService.SettlePayment(payment.PaymentMethod, paymentProductId, orderId);
				status = PaymentMessages.AcquireSuccess;
				return true;
			}
			catch (GlobalCollectException gce)
			{
				HandleGlobalCollectException(payment, gce, out status);
				return false;
			}
		}

		protected override bool RefundPaymentInternal(Payment payment, out string status)
		{
			var orderId = TryGetOrderIdFromPayment(payment);
			var merchantReference = GetMerchantReference(payment);
			var amount = (long)payment.Amount.ToCents();

			try
			{
				_globalCollectService.RefundPayment(payment.PaymentMethod, orderId, merchantReference, amount);
				status = PaymentMessages.RefundSuccess;
				return true;
			}
			catch (GlobalCollectException gce)
			{
				HandleGlobalCollectException(payment, gce, out status);
				return false;
			}
		}

		public override Payment Extract(HttpRequest httpRequest)
		{
			return _paymentExtractor.Extract(httpRequest);
		}

		private long TryGetOrderIdFromPayment(Payment payment)
		{
			var orderIdAsString = payment["OrderId"];
			long orderId;
			if (long.TryParse(orderIdAsString, out orderId))
			{
				return orderId;
			}

			throw new SecurityException(string.Format("Could not find the property 'OrderId' on the payment wiht reference id: '{0}'", payment.ReferenceId));
		}

		private static string GetMerchantReference(Payment payment)
		{
			return payment.ReferenceId;
		}

		private void HandleGlobalCollectException(Payment payment, GlobalCollectException gce, out string status)
		{
			var error = gce.Errors[0];
			var message = string.Format("Error from Global Collect: Code '{0}'. Message '{1}'", error.Code, error.Message);
			var logPrefix = string.Format("Payment merchant id: {0} ", GetMerchantReference(payment));
			_loggingService.Log<GlobalCollectPaymentMethodService>(logPrefix + message);
			status = message;
		}

		private PaymentStatusCode ConvertGlobalCollectOrderStatusToPaymentStatusCode(GlobalCollectOrderStatus status, PaymentStatusCode previous, out string message)
		{

			PaymentStatusCode code;
			switch (status)
			{
				case GlobalCollectOrderStatus.OrderWithAttempt:
					code = PaymentStatusCode.PendingAuthorization;
					message = "Pending authorization.";
					break;
				case GlobalCollectOrderStatus.OrderWithSuccessfulAttempt:
					code = PaymentStatusCode.Authorized;
					message = "Payment authorized (Order with successful attempt.";
					break;
				case GlobalCollectOrderStatus.CancelledByMerchant:
					code = PaymentStatusCode.Declined;
					message = "Cancelled by merchant";
					break;
				case GlobalCollectOrderStatus.RejectedByMerchant:
					code = PaymentStatusCode.Declined;
					message = "Rejected by merchant";
					break;
				case GlobalCollectOrderStatus.RefundCreated:
					code = PaymentStatusCode.Refunded;
					message = "Refund created";
					break;
				case GlobalCollectOrderStatus.RefundSuccessful:
					code = PaymentStatusCode.Refunded;
					message = "Refund successfull";
					break;
				case GlobalCollectOrderStatus.RefundFailed:
					code = previous;
					message = "Refund successfull";
					break;
				case GlobalCollectOrderStatus.EndedAutomatically:
					code = PaymentStatusCode.Cancelled;
					message = "Ended automatically";
					break;
				case GlobalCollectOrderStatus.EndedByMerchant:
					code = PaymentStatusCode.Cancelled;
					message = "Ended by merchant";
					break;
				case GlobalCollectOrderStatus.OrderCreated:
					code = PaymentStatusCode.PendingAuthorization;
					message = "Order created";
					break;
				case GlobalCollectOrderStatus.OrderSuccessful:
					code = PaymentStatusCode.Acquired;
					message = "Order successful";
					break;
				case GlobalCollectOrderStatus.OrderOpen:
					code = PaymentStatusCode.Authorized;
					message = "Order open";
					break;
				default:
					throw new SecurityException("Could not convert status " + status);
			}

			return code;
		}

		private IPaymentData CallInsertOrderWithPayment(PaymentRequest paymentRequest)
		{
			Guard.Against.Null(paymentRequest.Payment.PurchaseOrder.BillingAddress, "PurchaseOrder.BillingAddress must be supplied for Global Collect payments. Please make sure that you update the property prior to initiating payment either by using the API TransactionLibrary.EditBillingInformation() or setting the property directly.");
			var payment = paymentRequest.Payment;
			var paymentMethod = payment.PaymentMethod;
			var paymentProductId = TryGetPaymentProductIdFromPayment(payment);
			var billingAddress = paymentRequest.PurchaseOrder.BillingAddress;
			var shipment = paymentRequest.PurchaseOrder.Shipments.FirstOrDefault();
			var amount = paymentRequest.Amount.Value.ToCents();
			var currency = paymentRequest.Amount.Currency.ISOCode;
			var language = paymentRequest.Amount.Culture.TwoLetterISOLanguageName;
			var country = TryGetCountryFromCustomerOrPayment(paymentRequest);
			var merchantReference = GetMerchantReference(payment);
			var ipAddress = GetUserIpAddress();

			bool useAuthenticationIndicator = Force3DSecure(paymentProductId, paymentRequest);

			// NB! When I used the character "&" to add the "ADDITIONALREFERENCE" value, the parsing of the resulting XML would fail :-S
			// I changed it to "&amp;" instead, and it appears to be ok. But since we have never seen a response yet, I do not know if the value actually would be added.
			var returnUrl = AbstractPageBuilder.GetCallbackUrl(paymentMethod.DynamicProperty<string>().CallbackUrl, paymentRequest.Payment);

			var globalCollectBillingAddress = new GlobalCollect.Api.Parts.Address()
			{
				FirstName = billingAddress.FirstName,
				LastName = billingAddress.LastName,
				PhoneNumber = billingAddress.PhoneNumber,
				StreetLine1 = billingAddress.Line1,
				StreetLine2 = billingAddress.Line2,
				Zip = billingAddress.PostalCode,
				Email = billingAddress.EmailAddress,
				IpAddress = ipAddress,
				State = billingAddress.State,
				City = billingAddress.City,
				CompanyName = billingAddress.CompanyName,
				CountryCode = billingAddress.Country.TwoLetterISORegionName
			};

			var globalCollectShippingAddress = new GlobalCollect.Api.Parts.Address();

			if (shipment != null)
			{
				var shippingAddress = shipment.ShipmentAddress;
				if (shippingAddress != null)
				{
					globalCollectShippingAddress.FirstName = shippingAddress.FirstName;
					globalCollectShippingAddress.LastName = shippingAddress.LastName;
					globalCollectShippingAddress.StreetLine1 = shippingAddress.Line1;
					globalCollectShippingAddress.StreetLine2 = shippingAddress.Line2;
					globalCollectShippingAddress.Zip = shippingAddress.PostalCode;
					globalCollectShippingAddress.State = shippingAddress.State;
					globalCollectShippingAddress.Email = shippingAddress.EmailAddress;
					globalCollectShippingAddress.City = shippingAddress.City;
					globalCollectShippingAddress.CountryCode = shippingAddress.Country.TwoLetterISORegionName;
					globalCollectShippingAddress.CompanyName = shippingAddress.CompanyName;
				}
			}

			var createPaymentRequest = new CreatePaymentRequestDto(
												amount,
												currency,
												country,
												language,
												merchantReference,
												paymentProductId,
												returnUrl,
												globalCollectBillingAddress,
												globalCollectShippingAddress,
												useAuthenticationIndicator
											);

			var payments = _globalCollectService.CreatePayment(payment.PaymentMethod, createPaymentRequest).ToList();

			if (payments.Count() != 1)
			{
				throw new SecurityException("Did not receive the expected number of payments from Global Collect");
			}

			var paymentData = payments.Single();
			LogPaymentDataReceived(paymentData);

			payment["Mac"] = paymentData.Mac;
			payment["Ref"] = paymentData.Ref;
			payment["ReturnMac"] = paymentData.ReturnMac;
			payment["OrderId"] = paymentData.OrderId.ToString(CultureInfo.InvariantCulture);
			payment["PaymentProductId"] = paymentProductId.ToString(CultureInfo.InvariantCulture);
			payment["PaymentReference"] = paymentData.PaymentReference;
			payment.TransactionId = paymentData.OrderId.ToString(CultureInfo.InvariantCulture);

			payment.Save();

			return paymentData;
		}

		private bool Force3DSecure(int paymentProductId, PaymentRequest paymentRequest)
		{
			string listOfPaymentProducts =
				paymentRequest.PaymentMethod.DynamicProperty<string>().Force3DSecureForThesePaymentProducts;

			if (string.IsNullOrEmpty(listOfPaymentProducts)) return false;

			string[] parts = listOfPaymentProducts.Split(',');

			foreach (var part in parts)
			{
				int partValue;
				if (int.TryParse(part, out partValue))
				{
					if (partValue == paymentProductId)
					{
						// The selected payment product id was found in the list of values on the property.
						return true;
					}
				}
			}

			return false;
		}

		private string GetUserIpAddress()
		{
			string ipList = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

			if (!string.IsNullOrEmpty(ipList))
			{
				return ipList.Split(',')[0];
			}

			return HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
		}
		private void LogPaymentDataReceived(IPaymentData paymentData)
		{
			var message =
				string.Format(
					"Received payment data from Global Collect: OrderId '{0}', OrderStatus '{1}', FormMethod '{2}', FormAction '{3}'",
					paymentData.OrderId,
					paymentData.StatusId,
					paymentData.FormMethod,
					paymentData.FormAction);

			_loggingService.Log<GlobalCollectPaymentMethodService>(message);
		}

		private string TryGetCountryFromCustomerOrPayment(PaymentRequest paymentRequest)
		{
			if (!string.IsNullOrEmpty(paymentRequest.Payment["Country"]))
			{
				return paymentRequest.Payment["Country"];
			}

			if (paymentRequest.PurchaseOrder.BillingAddress != null && !string.IsNullOrWhiteSpace(paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName))
			{
				return paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName;
			}

			var paymentMethod = paymentRequest.Payment.PaymentMethod;
			return paymentMethod.DynamicProperty<string>().Country;
		}

		private int TryGetPaymentProductIdFromPayment(Payment payment)
		{
			var v = payment["PaymentProductId"];
			if (!string.IsNullOrEmpty(v))
			{
				int i;
				if (int.TryParse(v, out i))
				{
					return i;
				}
			}

			var paymentMethod = payment.PaymentMethod;
			// Fallback strategy.
			return paymentMethod.DynamicProperty<int>().DefaultPaymentProductId;
		}
	}
}

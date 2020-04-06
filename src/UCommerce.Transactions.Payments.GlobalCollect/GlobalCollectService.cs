using System;
using System.Collections.Generic;
using System.Configuration;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure.Globalization;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Transactions.Payments.GlobalCollect.Api;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect
{
	public class GlobalCollectService : IGlobalCollectService
	{
		private readonly ILoggingService _loggingService;
		private readonly ILanguageCodeMapper _languageCodeMapper;

		public GlobalCollectService(
			ILoggingService loggingService, ILanguageCodeMapper languageCodeMapper)
		{
			_loggingService = loggingService;
			_languageCodeMapper = languageCodeMapper;
		}

		public virtual IEnumerable<IPaymentData> CreatePayment(PaymentMethod paymentMethod, CreatePaymentRequestDto request)
		{
			var insertOrder = new InsertOrderWithPayment
			{
				Meta =
				{
					MerchantId = paymentMethod.DynamicProperty<int>().MerchantId
				},
				Order =
				{
					Amount = request.Amount, 
					CurrencyCode = request.CurrencyCode, 
					CountryCode = request.CountryCode, 
					LanguageCode = _languageCodeMapper.Convert(request.LanguageCode), 
					MerchantReference = request.MerchantReference,
					
					//Address fields
					BillingAddress = request.BillingAddress,
					ShippingAddress = request.ShippingAddress
				},
				Payment =
				{
					Amount = request.Amount, 
					CurrencyCode = request.CurrencyCode, 
					CountryCode = request.CountryCode, 
					LanguageCode = request.LanguageCode, 
					PaymentProductId = request.PaymentProductId,
					ReturnUrl = request.ReturnUrl,
					UseAuthenticationIndicator = request.UseAuthenticationIndicator
				}
			};

			var responseText = SendTextAndCheckResponseForErrors(GetServiceUrl(paymentMethod), insertOrder.ToString());

			var response = new InsertOrderWithPayment();
			response.FromModifiedXml(new ModifiedXmlDocument(responseText), string.Empty);

			return new List<PaymentData>(response.Response.PaymentRows);
		}

		public virtual IEnumerable<IPaymentProduct> GetPaymentProducts(PaymentMethod paymentMethod, string languageCode, string countryCode, string currencyCode)
		{
			languageCode = _languageCodeMapper.Convert(languageCode);

			var getPaymentProducts = new GetPaymentProducts
			{
				Meta = { MerchantId = paymentMethod.DynamicProperty<int>().MerchantId },
				General = { LanguageCode = languageCode, CountryCode = countryCode, CurrencyCode = currencyCode }
			};

			var responseText = SendTextAndCheckResponseForErrors(GetServiceUrl(paymentMethod), getPaymentProducts.ToString());

			var response = new GetPaymentProducts();
			response.FromModifiedXml(new ModifiedXmlDocument(responseText), string.Empty);

			return new List<IPaymentProduct>(response.Response.PaymentProducts);
		}

		public virtual void SettlePayment(PaymentMethod paymentMethod, int paymentProductId, long orderId)
		{
			var request = new SetPayment()
			{
				Meta = { MerchantId = paymentMethod.DynamicProperty<int>().MerchantId },
				Payment =
				{
					PaymentProductId = paymentProductId,
					OrderId = orderId
				}
			};

			SendTextAndCheckResponseForErrors(GetServiceUrl(paymentMethod), request.ToString());
		}

		public virtual void CancelPayment(PaymentMethod paymentMethod, long orderId, string merchantReference)
		{
			var orderStatus = GetOrderStatus(paymentMethod, orderId);
			var request = new CancelPayment()
			{
				Meta = { MerchantId = paymentMethod.DynamicProperty<int>().MerchantId },
				Payment =
				{
					OrderId = orderId,
					MerchantReference = merchantReference,
					EffortId = orderStatus.EffortId,
					AttemptId = orderStatus.AttemptId
				}
			};

			var text = request.ToString();
			SendTextAndCheckResponseForErrors(GetServiceUrl(paymentMethod), text);
		}

		public virtual void RefundPayment(PaymentMethod paymentMethod, long orderId, string merchantReference, long amount)
		{
			var request = new DoRefund()
			{
				Meta = { MerchantId = paymentMethod.DynamicProperty<int>().MerchantId },
				Payment =
				{
					OrderId = orderId,
					MerchantReference = merchantReference,
					Amount = amount
				}
			};

			SendTextAndCheckResponseForErrors(GetServiceUrl(paymentMethod), request.ToString());
		}

		public virtual IOrderStatus GetOrderStatus(PaymentMethod paymentMethod, long orderId)
		{
			var request = new GetOrderStatus()
			{
				Meta =
				{
					MerchantId = paymentMethod.DynamicProperty<int>().MerchantId, 
					Version = "2.0"
				},
				Order =
				{
					OrderId = orderId
				}
			};

			var text = request.ToString();
			var responseText = SendTextAndCheckResponseForErrors(GetServiceUrl(paymentMethod), text);

			var response = new GetOrderStatus();
			response.FromModifiedXml(new ModifiedXmlDocument(responseText), string.Empty);

			return response.Response.Status;
		}

		private string GetServiceUrl(PaymentMethod paymentMethod)
		{
			GlobalCollectSecurityCheck securityCheck;
			if (!Enum.TryParse(paymentMethod.DynamicProperty<string>().SecurityCheck, out securityCheck))
				throw new ConfigurationErrorsException(
					"Could not parse SecurityCheck value from payment method. Please check that your payment {0} has one of the following values set in the field 'SecurityCheck': {1} or {2}."
					.FormatWith(paymentMethod.Name,
					GlobalCollectSecurityCheck.IpCheck.ToString(),
					GlobalCollectSecurityCheck.ClientAuthentication.ToString()));

			if (paymentMethod.DynamicProperty<bool>().Live)
			{
				// Live
				switch (securityCheck)
				{
					case GlobalCollectSecurityCheck.IpCheck:
						return GlobalCollectConstants.LiveApiServiceUsingIpRestriction;
					case GlobalCollectSecurityCheck.ClientAuthentication:
						return GlobalCollectConstants.LiveApiServiceUsingClientAuthentication;
					default:
						throw new Exception("Invalid security value: " + securityCheck);
				}
			}

			// Debug
			switch (securityCheck)
			{
				case GlobalCollectSecurityCheck.IpCheck:
					return GlobalCollectConstants.TestApiServiceUsingIpRestriction;
				case GlobalCollectSecurityCheck.ClientAuthentication:
					return GlobalCollectConstants.TestApiServiceUsingClientAuthentication;
				default:
					throw new Exception("Invalid security value: " + securityCheck);
			}
		}

		private string SendTextAndCheckResponseForErrors(string url, string text)
		{
			var caller = new ServiceApiCaller(url);

			_loggingService.Log<GlobalCollectService>("Sending: " + text);
			var responseText = caller.Send(text);
			_loggingService.Log<GlobalCollectService>("Receiving: " + responseText);

			var checker = new ErrorChecker(responseText);
			if (checker.Result != "OK")
			{
				_loggingService.Log<GlobalCollectService>(responseText);

				if (checker.Errors.Count == 1)
				{
					if (checker.Errors[0].Code == 410120)
					{
						throw new PaymentAmountOutOfRangeExeption(checker);
					}
				}
				throw new GlobalCollectException(checker);
			}

			return responseText;
		}
	}
}

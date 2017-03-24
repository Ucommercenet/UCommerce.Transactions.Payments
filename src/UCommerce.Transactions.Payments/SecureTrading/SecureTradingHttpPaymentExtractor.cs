using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using UCommerce.EntitiesV2;

namespace UCommerce.Transactions.Payments.SecureTrading
{
	/// <summary>
	/// Responsible for extracting a payment origin from secure trading.
	/// </summary>
	public class SecureTradingHttpPaymentExtractor : IHttpPaymentExtractor
	{
		private readonly IRepository<Payment> _paymentRepository;

		public SecureTradingHttpPaymentExtractor(IRepository<Payment> paymentRepository)
		{
			_paymentRepository = paymentRepository;
		}

		/// <summary>
		/// Extract the payment from the payment request
		/// </summary>
		/// <param name="httpRequest"></param>
		/// <returns>Payment extracted</returns>
		/// <remarks>
		/// Secure trading have the possibility to pass along custom fields that are sent to the gateway in order to recognize the payment.
		/// We've setup the configuration to return paymentrefrence - which is sent along in the payment form.
		/// </remarks>
		public Payment Extract(HttpRequest httpRequest)
		{
			var paramenter = httpRequest.QueryString[SecureTradingConstants.PaymentReference];

			if (string.IsNullOrWhiteSpace(paramenter)) 
				throw new ConfigurationErrorsException(
					"paymentreference not recieved from Secure Trading. Make sure to configure Secure Trading to send 'paymentreference' in the redirect.");

			return _paymentRepository.SingleOrDefault(x => x.PaymentProperties.Any(y => y.Value == paramenter));
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;

namespace UCommerce.Transactions.Payments.Adyen
{
	/// <summary>
	/// Responsible for extracting payment from Adyen requests.
	/// </summary>
	public class AdyenHttpPaymentExtractor : IHttpPaymentExtractor
	{
		private const string ParameterName = "merchantReference";
		
		/// <summary>
		/// Extracts the specified HTTP request.
		/// </summary>
		/// <param name="httpRequest">The HTTP request.</param>
		/// <returns></returns>
		public Payment Extract(HttpRequest httpRequest)
		{
			var reference = httpRequest[ParameterName];

			if (string.IsNullOrEmpty(reference))
			{
				return EmptyPayment(reference);
			}

			Payment payment = Payment.All().SingleOrDefault(x => x.ReferenceId == reference);

		    if (payment == null)
		    {
			    return EmptyPayment(reference);
		    }

		    return payment;
		}

		private Payment EmptyPayment(string reference)
		{
			if (string.IsNullOrWhiteSpace(reference))
				reference = " ";

			var payment = new Payment();
			payment["EmptyPayment"] = "true";
			payment["ReferenceId"] = reference;

			return payment;
		}
	}
}

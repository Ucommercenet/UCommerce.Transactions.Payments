using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public class CreatePaymentRequestDto
	{
		public CreatePaymentRequestDto
			(
				long amount, string currencyCode, string countryCode, string languageCode, 
				string merchantReference, int paymentProductId, string returnUrl, Address billingAddress,Address shippingAddress, bool useAuthenticationIndicator
			)
		{
			ExtraPaymentParameters = new List<KeyValuePair<string, string>>();
			ExtraOrderParameter = new List<KeyValuePair<string, string>>();

			Amount = amount;
			CurrencyCode = currencyCode;
			CountryCode = countryCode;
			LanguageCode = languageCode;
			MerchantReference = merchantReference;
			PaymentProductId = paymentProductId;
			ReturnUrl = returnUrl;
			BillingAddress = billingAddress;
			ShippingAddress = shippingAddress;
			UseAuthenticationIndicator = useAuthenticationIndicator;
		}

		public long Amount { get; private set; }

		public string CurrencyCode { get; private set; }

		public string CountryCode { get; private set; }

		public string LanguageCode { get; private set; }

		public string MerchantReference { get; private set; }

		public int PaymentProductId { get; private set; }

		public string ReturnUrl { get; private set; }
		
		//Address fields
		public Address ShippingAddress { get; private set; }

		public Address BillingAddress { get; private set; }

		public IList<KeyValuePair<string, string>> ExtraPaymentParameters { get; private set; }

		public IList<KeyValuePair<string, string>> ExtraOrderParameter { get; private set; }

		// Use 3D secure;
		public bool UseAuthenticationIndicator { get; private set; }
	}
}

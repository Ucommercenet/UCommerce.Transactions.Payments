using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Logging;

namespace UCommerce.Transactions.Payments.Adyen
{
	public class PaymentResultValidator
	{
		private readonly string[] _fieldsInSignaturePaymentResult =
			{
				"authResult",
				"pspReference",
				"merchantReference",
				"skinCode",
				"merchantReturnData"
			};

		protected ILoggingService LoggingService { get; set; }

		public PaymentResultValidator(ILoggingService loggingService)
		{
			LoggingService = loggingService;
		}

		private string BuildSigningString(IDictionary<string, string> dict)
		{
			return _fieldsInSignaturePaymentResult.Where(dict.ContainsKey).Aggregate(string.Empty, (current, fieldName) => current + dict[fieldName]);
		}

		private string BuildSigningStringForSHA256(IDictionary<string, string> dict)
		{
			Dictionary<string, string> signDict = dict.Where(x => x.Key != "merchantSig").OrderBy(d => d.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
			string keystring = string.Join(":", signDict.Keys);
			string valuestring = string.Join(":", signDict.Values.Select(EscapeValue));
			return string.Format("{0}:{1}", keystring, valuestring);
		}

		private string EscapeValue(string value)
		{
			if (value == null)
			{
				return string.Empty;
			}

			value = value.Replace(@"\", @"\\");
			value = value.Replace(":", @"\:");
			return value;
		}

		private string CalculateMerchantSignature(IDictionary<string, string> dict, PaymentMethod paymentMethod)
		{
            string signature;

			if (paymentMethod.DynamicProperty<string>().SigningAlgorithm == "SHA256")
			{
				var signingString = BuildSigningStringForSHA256(dict);
				var calculator = new HmacCalculatorSHA256(HttpUtility.UrlDecode(paymentMethod.DynamicProperty<string>().HmacSharedSecret));
				signature = calculator.Execute(signingString);
			}
			else
			{
				var signingString = BuildSigningString(dict);
				var calculator = new HmacCalculator(HttpUtility.UrlDecode(paymentMethod.DynamicProperty<string>().HmacSharedSecret));
				signature = calculator.Execute(signingString);
			}


			return signature;
		}

	    public bool NotificationMessageIsAuthenticated(PaymentMethod paymentMethod)
	    {
            var currentRequest = System.Web.HttpContext.Current.Request;

	        if (!currentRequest.IsSecureConnection)
	        {
	            LoggingService.Log<PaymentResultValidator>("Adyen uses basic auth for sending payment notification. Request validated as insecure. Has your application been configured for SSL?");
	        }

	        string authHeader = currentRequest.Headers["Authorization"];

            string userName = paymentMethod.DynamicProperty<string>().NotificationUsername;
            string password = paymentMethod.DynamicProperty<string>().NotificationPassword;
        
            if (authHeader == null)
            {
                return false;
            }
            else
            {
                string encodedAuth = authHeader.Split(' ')[1];
                string decodedAuth = Encoding.UTF8.GetString(Convert.FromBase64String(encodedAuth));

                var requestUser = decodedAuth.Split(':')[0];
                var requestPassword = decodedAuth.Split(':')[1];

                if (!userName.Equals(requestUser) || !password.Equals(requestPassword))
                {
                    return false;
                }
            }

	        return true;
	    }

        public bool ValidateSignature(IDictionary<string, string> dict, PaymentMethod paymentMethod)
		{
			if (!dict.ContainsKey("merchantSig"))
			{
				LoggingService.Log<PaymentResultValidator>("Dictionary does not contain the key 'merchantSig'");
				return false;
			}

			var signature = dict["merchantSig"];
			var referenceSignature = CalculateMerchantSignature(dict, paymentMethod);

			if (signature != referenceSignature)
			{
				LoggingService.Log<PaymentResultValidator>(string.Format("The calculated signature '{0}' does not match the expected '{1}'.", signature, referenceSignature));
				return false;
			}

			return true;
		}
	}
}

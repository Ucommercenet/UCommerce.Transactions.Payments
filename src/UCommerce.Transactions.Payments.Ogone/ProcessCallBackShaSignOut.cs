using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Ucommerce.Transactions.Payments.Ogone
{
	/// <summary>
	/// Class used to build a string to hash when Ogone does a callback to our server.
	/// Ogone needs a "sha sign" which is a signature used to verify that the data is identically in both ends.
	/// The "sha sign" is a sha-1 hash of the string this class builds.
	/// </summary>
	public class ProcessCallBackShaSignOut
	{

		/// <summary>
		/// Builds the string to hash when Ogone performs the callback at "paymentProccessor.axd"
		/// </summary>
		/// <param name="context">The context contains the parameters send to the "paymentProccessor.axd".</param>
		/// <param name="shaSignOut">The ShaSignOut as configured in the Ogone back-end.</param>
		/// <returns>The string to be hashed.</returns>
		public string BuildHashString(HttpContext context, string shaSignOut)
		{
			var keysOfInterest = new List<string>
			                 	{
			                 		"AAVADDRESS","AAVCHECK","AAVMAIL","AAVNAME","AAVPHONE","AAVZIP","ACCEPTANCE","AMOUNT",
									"BIC","BIN","BRAND","CARDNO","CCCTY","CN","COLLECTOR_BIC","COLLECTOR_IBAN","COMPLUS",
									"CREDITDEBIT","CURRENCY","CVCCHECK","ECI","ED","EMAIL",
									"FXAMOUNT","FXCURRENCY","IP","IPCTY","MANDATEID","MOBILEMODE",
									"NCERROR","ORDERID","PAYID","PAYIDSUB", "PAYLIBIDREQUEST","PAYLIBTRANSID","PAYMENT_REFERENCE","PM",
									"SEQUENCETYPE","SIGNDATE","STATUS","SUBBRAND","TRXDATE","VC", "WALLET"
			                 	};

			//Local comparison will treat the letters as fx AA = Å 
			//Therefore, it's necessary to ignore culture case. 
			keysOfInterest.Sort(StringComparer.InvariantCultureIgnoreCase);

			var concatString = new StringBuilder();

			foreach (string key in keysOfInterest)
				concatString.Append(BuildStringSection(key, context, shaSignOut));

			return concatString.ToString();
		}

		/// <summary>
		/// Builds a section of the string to hash. 
		/// Method helps to match the rules of building a sha sign given by Ogone.
		/// Keys must be in upper and empty values must be ignored.
		/// </summary>
		/// <param name="parameter">The parameter.</param>
		/// <param name="context">The context.</param>
		/// <param name="shaSign">The sha sign.</param>
		/// <returns>A string section in "KEY=value" format.</returns>
		private string BuildStringSection(string parameter, HttpContext context, string shaSign)
		{
			string value = context.Request[parameter];
			if (string.IsNullOrEmpty(value))
				return "";
			return parameter.ToUpper() + "=" + HttpUtility.UrlDecode(value) + shaSign;
		}

	}
}

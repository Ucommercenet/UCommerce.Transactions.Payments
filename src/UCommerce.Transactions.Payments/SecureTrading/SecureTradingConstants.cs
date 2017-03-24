using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.SecureTrading
{
	/// <summary>
	/// Constants for looking for different parameters in querystring callback and building the request in pagebuilder.
	/// </summary>
	public static class SecureTradingConstants
	{
		public static string AuthRequestParameter { get { return "AuthRequestParameter"; } } //custom field to be included in request for returning again in querystring.
		public static string InstantCapture { get { return "InstantCapture"; } }
		public static string Authorize { get { return "Authorize"; } }
		public static string ErrorCode { get { return "errorcode"; } }
		public static string Transactionreference { get { return "transactionreference"; } }
		public static string PaymentReference { get { return "paymentreference"; } }


	}
}

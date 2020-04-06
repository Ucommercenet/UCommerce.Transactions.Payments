using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect
{
	public static class PaymentStatusHelper
	{
		public static string ConvertPaymentStatusCodeToHumanReadableMessage(int message)
		{
			switch (message)
			{
				case 0:
					return "READY";
				case 20:
					return "PENDING AT MERCHANT";
				case 25:
				case 30:
					return "PENDING AT GLOBAL COLLECT";
				case 50:
					return "PENDING AT BANK or ENROLLED";
				case 55:
					return "PENDING AT CONSUMER";
				case 60:
					return "NOT ENROLLED";
				case 65:
					return "PENDING PAYMENT (CONSUMER AT BANK)";
				case 70:
					return "BANK IS IN DOUBT";
				case 100:
					return "REJECTED";
				case 120:
					return "REJECTED BY BANK";
				case 125:
					return "CANCELLED AT BANK";
				case 130:
					return "FAILED";
				case 140:
					return "EXPIRED AT BANK";
				case 150:
					return "TIMED OUT AT BANK";
				case 160:
					return "DENIED";
				case 170:
					return "AUTHORISATION EXPIRED";
				case 172:
					return "AUTHENTICATION ENROLLEMENT EXPIRED";
				case 175:
					return "AUTHENTICATION VALIDATION EXPIRED";
				case 180:
					return "INVALID PARES OR NOT COMPLETED";
				case 190:
					return "SETTLEMENT REJECTED";
				case 200:
					return "CARDHOLDER AUTHENTICATED";
				case 220:
					return "COULD NOT AUTHENTICATE";
				case 230:
					return "CARDHOLDER NOT PARTICIPATING";
				case 280:
					return "INVALID PARES OR NOT COMPLETED";
				case 300:
					return "AUTHORIZATION TESTED";
				case 310:
					return "NOT ENROLLED";
				case 320:
					return "COULD NOT AUTHENTICATE";
				case 330:
					return "CARDHOLDER NOT PARTICIPATING";
				case 350:
					return "CARDHOLDER AUTHENTICATED";
				case 400:
					return "REVISED";
				case 500:
					return "FINAL";
				case 525:
					return "CHALLENGED";
				case 550:
					return "REFERRED";
				case 600:
					return "PENDING";

				default:
					return string.Empty;
			}
		}
	}
}

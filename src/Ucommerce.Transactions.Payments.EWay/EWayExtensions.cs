using System;
using System.Linq;
using System.Xml.Linq;
using UCommerce.Infrastructure;

namespace UCommerce.Transactions.Payments.EWay
{
	/// <summary>
	/// Extensions for use in the EWay payment provider.
	/// </summary>
	public static class EWayExtensions
	{
		public static void TransactionNotAccepted(this Guard guard, bool transactionAccepted, XDocument result)
		{
			if (!transactionAccepted)
			{
				var errorCode = result.Descendants("Error").FirstOrDefault();
				var value = "";
				if (errorCode != null)
					value = errorCode.Value;
				throw new InvalidOperationException(String.Format("Could not create placeholder transaction with EWay: {0}.", value));
			}	
		}

		public static void EmptyRedirectUrl(this Guard guard, string url)
		{
			if (string.IsNullOrEmpty(url))
				throw new InvalidOperationException("Could not redirect to Eway Payment page.");
		}
	}
}

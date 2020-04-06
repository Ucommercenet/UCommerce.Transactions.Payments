using System;
using System.Configuration;
using Ucommerce.Extensions;

namespace Ucommerce.Transactions.Payments.Adyen
{
	public static class EnumExtensions
	{
		public static AdyenPaymentFlowSelection ParseFlowSelectionThrowExceptionOnFailure(this string val)
		{
			AdyenPaymentFlowSelection fs;
			if (Enum.TryParse(val, out fs))
			{
				return fs;
			}
			throw new ConfigurationErrorsException(
				"Could not parse FlowSelection value '{0}' from payment method. Please check that your payment has one of the following values set in the field 'FlowSelection': {1}, {2} or {3}."
					.FormatWith(val,
						AdyenPaymentFlowSelection.OnePage.ToString(),
						AdyenPaymentFlowSelection.MultiplePage.ToString(),
						AdyenPaymentFlowSelection.DirectoryLookup.ToString()));
		}
	}
}

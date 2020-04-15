using System;
using Ucommerce.Infrastructure;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Netaxept
{
    public static class CurrencyExtension
    {
        public static void UnsupportedCurrencyInNetaxept(this Guard g, Currency billingCurrency)
        {
            bool result = false;
            switch (billingCurrency.ISOCode)
            {
                case "DKK":
                    result = true;
                    break;

                case "EUR":
                    result = true;
                    break;

                case "NOK":
                    result = true;
                    break;

                case "SEK":
                    result = true;
                    break;

                case "USD":
                    result = true;
                    break;
            }

            if (!result)
				//TODO: LCH, Add all supported currencies to err message
                throw new InvalidOperationException(string.Format("Netaxept doesn't support {0} as currency. To use Netaxept please change billing currency of order to EUR or USD.", billingCurrency.ISOCode));
        }
    }
}

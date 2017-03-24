using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public interface IPaymentProduct
	{
		string PaymentMethodName { get; }

		int PaymentProductId { get; }
	
		string PaymentProductName { get; }
		
		long? MinimumAmount { get; }
		
		long? MaximumAmount { get; }
		
		string CurrencyCode { get; }
		
		int OrderTypeIndicator { get; }
		
		string PaymentProductLogo { get; }
	}
}

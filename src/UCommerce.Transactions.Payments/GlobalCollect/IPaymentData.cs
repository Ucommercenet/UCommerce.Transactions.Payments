using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public interface IPaymentData
	{
		long StatusDate { get; }
	
		string PaymentReference { get; }
		
		string AdditionalReference { get; }
		
		long OrderId { get; }
		
		string ExternalReference { get; }
		
		int EffortId { get; }
		
		string Ref { get; }
		
		string FormAction { get; }
		
		string FormMethod { get; }
		
		int AttemptId { get; }
		
		int MerchantId { get; }
		
		int StatusId { get; }
		
		string ReturnMac { get; }
		
		string Mac { get; }
	}
}

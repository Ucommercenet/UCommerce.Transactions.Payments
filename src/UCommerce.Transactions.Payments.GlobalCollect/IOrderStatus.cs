using System.Collections.Generic;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect
{
	public interface IOrderStatus
	{
		string StatusDate { get; set; }
		
		int PaymentMethodId { get; set; }
		
		string MerchantReference { get; set; }
		
		int AttemptId { get; set; }
		
		string PaymentReference { get; set; }
		
		long Amount { get; set; }
		
		int MerchantId { get; set; }
		
		long OrderId { get; set; }
		
		int StatusId { get; set; }
		
		int EffortId { get; set; }
		
		string CurrencyCode { get; set; }
		
		int PaymentProductId { get; set; }

		IList<StatusErrorRow> Errors { get; } 
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ucommerce.Transactions.Payments.Stripe
{
    public class StripeStatus
    {
		public const string Succeeded = "succeeded";
		public const string Pending = "pending";
		public const string Failed = "failed";
		public const string Canceled = "canceled";
		public const string Processing = "processing";
		public const string RequiresAction = "requires_action";
		public const string RequiresCapture = "requires_capture";
		public const string RequiresConfirmation = "requires_confirmation";
		public const string RequiresPaymentMethod = "requires_payment_method";
	}
}

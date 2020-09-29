using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments;
using Ucommerce.Web;

namespace UCommerce.Providers.StripeNet
{
	public class StripePageBuilder : AbstractPageBuilder
    {
	    protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
	    {
		    throw new NotImplementedException();
	    }
    }
}

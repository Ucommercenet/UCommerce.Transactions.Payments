using System.Text;
using System.Web;
using Stripe;
using Ucommerce.Extensions;
using Ucommerce.Web;
using File = System.IO.File;

namespace Ucommerce.Transactions.Payments.StripeNet
{
	public class StripePageBuilder : AbstractPageBuilder
    {
	    private readonly ICallbackUrl _callbackUrl;

	    public StripePageBuilder(ICallbackUrl callbackUrl)
	    {
		    _callbackUrl = callbackUrl;
	    }

	    protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
	    {
			// create client
			string apiKey = paymentRequest.PaymentMethod.DynamicProperty<string>().ApiKey;
			string apiSecret = paymentRequest.PaymentMethod.DynamicProperty<string>().ApiSecret;
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;
			var client = new StripeClient(apiKey);
			var paymentIntentService = new PaymentIntentService(client);

			string paymentFormTemplate = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentFormTemplate;
		    var allLines = File.ReadAllLines(HttpContext.Current.Server.MapPath(paymentFormTemplate));   
		    foreach (var line in allLines)
		    {
			    page.AppendLine(line);
		    }

		    string paymentIntent = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentIntentID;


	    }
    }
}

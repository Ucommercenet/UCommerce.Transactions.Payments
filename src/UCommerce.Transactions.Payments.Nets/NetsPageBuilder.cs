using System.Collections.Generic;
using System.Text;
using Ucommerce.Content;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Nets
{
    public class NetsPageBuilder : AbstractPageBuilder
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;
	    private readonly ICallbackUrl _callbackUrl;
	    private IDomainService DomainService { get; set; }

        public NetsPageBuilder(IDomainService domainService, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl)
        {
	        _absoluteUrlService = absoluteUrlService;
	        _callbackUrl = callbackUrl;
	        DomainService = domainService;
        }

    	protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append("<title>Nets</title>");
            page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
            if(!Debug)
                page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
        }

		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			string merchant = paymentRequest.PaymentMethod.DynamicProperty<string>().Merchant;

			string language = paymentRequest.PaymentMethod.DynamicProperty<string>().Language;
			string termsUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().TermsUrl;
			string merchantTermsUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().MerchantTermsUrl;

			string testSecretKey = paymentRequest.PaymentMethod.DynamicProperty<string>().TestSecretKey;
			string liveSecretKey = paymentRequest.PaymentMethod.DynamicProperty<string>().LiveSecretKey;

			string acceptUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string cancelUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CancelUrl;
			string callBackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;

			string paymentMethods = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentMethods;
			bool autoCapture = paymentRequest.PaymentMethod.DynamicProperty<bool>().AutoCapture;
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;

			var currency = paymentRequest.Amount.CurrencyIsoCode;
			string amount = paymentRequest.Payment.Amount.ToCents().ToString();

			var parametersToReturn = new Dictionary<string, string>();

			parametersToReturn["merchant"] = merchant;
			parametersToReturn["accepturl"] = _absoluteUrlService.GetAbsoluteUrl(acceptUrl);
			
			if (!string.IsNullOrWhiteSpace(cancelUrl))
				parametersToReturn["cancelurl"] = _absoluteUrlService.GetAbsoluteUrl(cancelUrl);

			parametersToReturn["orderGuid"] = paymentRequest.Payment.PurchaseOrder.OrderGuid.ToString();
			parametersToReturn["currency"] = currency;
			//parametersToReturn["language"] = GetTwoLetterLanguageName();

			parametersToReturn["callbackurl"] = _callbackUrl.GetCallbackUrl(callBackUrl, paymentRequest.Payment);

			if (testMode)
				parametersToReturn["test"] = "1";

			parametersToReturn["orderid"] = paymentRequest.Payment.ReferenceId;
			parametersToReturn["amount"] = amount;

			return parametersToReturn;
		}

	    protected virtual string GetTwoLetterLanguageName()
	    {
			Domain domain = DomainService.GetCurrentDomain();
		    return domain != null
			    ? domain.Culture.TwoLetterISOLanguageName
			    : System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
	    }

		protected virtual void AddParameters(StringBuilder page, IDictionary<string,string> parametersToAdd)
		{	
			foreach (var parameter in parametersToAdd)
			{
				AddHiddenField(page,parameter.Key,parameter.Value);
			}

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");
	    }

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append(@"<form method=""post"" action=""https://payment.architrade.com/paymentweb/start.action"">");

	        var parameters = GetParameters(paymentRequest);
			AddParameters(page,parameters);

            page.Append("</form>");
        }
    }
}

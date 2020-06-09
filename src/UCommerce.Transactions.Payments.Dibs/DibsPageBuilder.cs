using System.Collections.Generic;
using System.Text;
using Ucommerce.Content;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Dibs
{
    public class DibsPageBuilder : AbstractPageBuilder
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;
	    private readonly ICallbackUrl _callbackUrl;
	    private IDomainService DomainService { get; set; }
    	private DibsMd5Computer DibsMd5Computer { get; set; }

        public DibsPageBuilder(IDomainService domainService, DibsMd5Computer dibsMd5Computer, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl)
        {
	        _absoluteUrlService = absoluteUrlService;
	        _callbackUrl = callbackUrl;
	        DomainService = domainService;
        	DibsMd5Computer = dibsMd5Computer;
        }

    	protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append("<title>Dibs</title>");
            page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
            if(!Debug)
                page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
        }

		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			string decorator = paymentRequest.PaymentMethod.DynamicProperty<string>().Decorator;
			string merchant = paymentRequest.PaymentMethod.DynamicProperty<string>().Merchant;
			string key1 = paymentRequest.PaymentMethod.DynamicProperty<string>().Key1;
			string key2 = paymentRequest.PaymentMethod.DynamicProperty<string>().Key2;
			string acceptUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string cancelUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CancelUrl;
			string callBackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string payType = paymentRequest.PaymentMethod.DynamicProperty<string>().PayType;
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;
			bool calculateFee = paymentRequest.PaymentMethod.DynamicProperty<bool>().CalculateFee;
			bool useMd5 = paymentRequest.PaymentMethod.DynamicProperty<bool>().UseMd5;

			var currency = paymentRequest.Amount.CurrencyIsoCode;
			string amount = paymentRequest.Payment.Amount.ToCents().ToString();

			var parametersToReturn = new Dictionary<string, string>();

			parametersToReturn["submitBasket"] = "yes";
			parametersToReturn["decorator"] = decorator;
			parametersToReturn["merchant"] = merchant;
			parametersToReturn["accepturl"] = _absoluteUrlService.GetAbsoluteUrl(acceptUrl);
			
			if (!string.IsNullOrWhiteSpace(cancelUrl))
				parametersToReturn["cancelurl"] = _absoluteUrlService.GetAbsoluteUrl(cancelUrl);

			parametersToReturn["orderGuid"] = paymentRequest.Payment.PurchaseOrder.OrderGuid.ToString();
			parametersToReturn["currency"] = currency;
			parametersToReturn["lang"] = GetTwoLetterLanguageName();

			if (payType != "ALL_CARDS" && !string.IsNullOrWhiteSpace(payType))
				parametersToReturn["paytype"] = payType;

			parametersToReturn["callbackurl"] = _callbackUrl.GetCallbackUrl(callBackUrl, paymentRequest.Payment);

			if (testMode)
				parametersToReturn["test"] = "1";

			if (calculateFee)
				parametersToReturn["calcfee"] = "1";

			parametersToReturn["orderid"] = paymentRequest.Payment.ReferenceId;
			parametersToReturn["amount"] = amount;

			if (useMd5)
			{
				parametersToReturn["uniqueoid"] = "yes";
				parametersToReturn["md5key"] = DibsMd5Computer.GetPreMd5Key(paymentRequest.Payment.ReferenceId, currency, amount, key1, key2, merchant);
			}

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

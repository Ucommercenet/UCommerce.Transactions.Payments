using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure.Configuration;
using Ucommerce.Infrastructure.Globalization;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Quickpay
{
    /// <summary>
    /// Builds a Quickpay redirection page
    /// </summary>
    public class QuickpayPageBuilder : AbstractPageBuilder
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;
	    private readonly ICallbackUrl _callbackUrl;
	    private CustomGlobalization LocalizationContext { get; set; }
		private QuickpayMd5Computer Md5Computer { get; set; }

        private const string PROTOCOL = "6";

		/// <summary>
		/// Initializes a new instance of the <see cref="QuickpayPageBuilder"/> class.
		/// </summary>
		public QuickpayPageBuilder(QuickpayMd5Computer md5Computer,IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl)
		{
			_absoluteUrlService = absoluteUrlService;
			_callbackUrl = callbackUrl;
			LocalizationContext = new CustomGlobalization();
			Md5Computer = md5Computer;
		}

        protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
        {
			page.Append("<title>Quickpay</title>");
            page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			
			if (!Debug)
                page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
        }

        /// <summary>
        /// Adds the language to the <see cref="StringBuilder"/>
        /// </summary>
        protected virtual string GetTwoLetterLanguageCode(PaymentRequest paymentRequest)
        {
			var culture = paymentRequest.Payment != null && paymentRequest.Payment.PurchaseOrder != null &&
			  paymentRequest.PurchaseOrder.CultureCode != null
				  ? new CultureInfo(paymentRequest.Payment.PurchaseOrder.CultureCode)
				  : new CultureInfo("en-us");

			// Culture based on order.
			// It's used in the AddLanguage call.
			LocalizationContext.SetCulture(culture);
			var cultureCode = LocalizationContext.CurrentCultureCode;

            var languageCode = "en";
			switch (cultureCode)
            {
                case "da":
                case "da-DK":
                    languageCode = "da";
                    break;
                case "de":
                case "de-DE":
                    languageCode = "de";
                    break;
                case "es":
                case "es-ES":
                    languageCode = "es";
                    break;
                case "fo":
                case "fo-FO":
                    languageCode = "fo";
                    break;
                case "fi":
                case "fi-FI":
                    languageCode = "fi";
                    break;
                case "fr":
                case "fr-FR":
                    languageCode = "fr";
                    break;
                case "it":
                case "it-IT":
                    languageCode = "it";
                    break;
                case "no":
                case "nb-NO":
                case "nn-NO":
                    languageCode = "no";
                    break;
                case "nl":
                case "nl-NL":
                    languageCode = "nl";
                    break;
                case "pl":
                case "pl-PL":
                    languageCode = "pl";
                    break;
                case "ru":
                case "ru-RU":
                    languageCode = "ru";
                    break;
                case "sv":
                case "sv-SE":
                    languageCode = "sv";
                    break;
            }

            return languageCode;
        }

        /// <summary>
        /// Builds the form.
        /// </summary>
        /// <param name="page">The <see cref="StringBuilder" />.</param>
        /// <param name="paymentRequest">The payment request.</param>
        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        { 
			page.Append(@"<form id=""Quickpay"" name=""Quickpay"" method=""post"" action=""https://secure.quickpay.dk/form/"">");

	        var parameters = GetParameters(paymentRequest);
			AddParameters(page, parameters);

	        page.Append("</form>");
        }

		/// <summary>
		/// Returns the parameters needed to be posted with the form to initiate the payment at QuickPay.
		/// </summary>
		/// <param name="paymentRequest"></param>
		/// <returns></returns>
	    protected virtual IDictionary<string,string> GetParameters(PaymentRequest paymentRequest)
	    {
			string md5Secret = paymentRequest.PaymentMethod.DynamicProperty<string>().Md5secret;
			string merchant = paymentRequest.PaymentMethod.DynamicProperty<string>().Merchant;
			string acceptUrlConfig = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string callBackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string cancelUrlConfig = paymentRequest.PaymentMethod.DynamicProperty<string>().CancelUrl;
			bool instantAcquire = paymentRequest.PaymentMethod.DynamicProperty<bool>().InstantAcquire;
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;
		    
			var twoLetterLanguageCode = GetTwoLetterLanguageCode(paymentRequest);
		    var amount = paymentRequest.Payment.Amount.ToCents().ToString();
		    var acceptUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrlConfig)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
		    var cancelUrl = new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrlConfig)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			var callbackUrl = _callbackUrl.GetCallbackUrl(callBackUrl, paymentRequest.Payment);

		    var parametersToAdd = new Dictionary<string, string>();

		    parametersToAdd["protocol"] = PROTOCOL;
			parametersToAdd["msgtype"] = "authorize";
		    parametersToAdd["merchant"] = merchant;
		    parametersToAdd["language"] = twoLetterLanguageCode;
			parametersToAdd["ordernumber"] = paymentRequest.Payment.ReferenceId;
		    parametersToAdd["amount"] = amount;
		    parametersToAdd["currency"] = paymentRequest.Amount.CurrencyIsoCode;
		    parametersToAdd["continueurl"] = acceptUrl;
		    parametersToAdd["cancelurl"] = cancelUrl;
			parametersToAdd["callbackurl"] = callbackUrl;
		    parametersToAdd["autocapture"] = (instantAcquire ? "1" : "0");
			parametersToAdd["testmode"] = (testMode ? "1" : "0");

		    parametersToAdd["md5check"] = Md5Computer.GetPreMd5Key(
			    PROTOCOL,
			    twoLetterLanguageCode,
			    paymentRequest.Payment.ReferenceId,
			    amount,
			    paymentRequest.Amount.CurrencyIsoCode,
			    acceptUrl,
			    cancelUrl,
			    callbackUrl,
			    instantAcquire ? "1" : "0",
			    md5Secret,
			    merchant,
			    testMode ? "1" : "0"
			    );

		    return parametersToAdd;
	    }

		/// <summary>
		/// Appends the parameters to the stringBuilder that needs to be posted to the form to initiate the payment at QuickPay.
		/// </summary>
		/// <param name="page"></param>
		/// <param name="parametersToAdd"></param>
	    protected virtual void AddParameters(StringBuilder page, IDictionary<string,string> parametersToAdd)
	    {
		    foreach (var parameter in parametersToAdd)
		    {
			    AddHiddenField(page, parameter.Key, parameter.Value);
		    }
			
		    if (Debug)
			    AddSubmitButton(page, "ac", "Post it");
	    }
    }
}

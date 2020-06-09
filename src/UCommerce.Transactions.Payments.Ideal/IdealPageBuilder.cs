using System;
using System.Collections.Generic;
using System.Text;
using Ucommerce.Extensions;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Ideal
{
    public class IdealPageBuilder : AbstractPageBuilder
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;

	    public IdealPageBuilder(IAbsoluteUrlService absoluteUrlService)
	    {
		    _absoluteUrlService = absoluteUrlService;
	    }

	    protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append("<title>iDEAL</title>");
            page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
            if (!Debug)
                page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
        }

	    protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
	    {
		    var paymentMethod = paymentRequest.PaymentMethod;

		    string merchantId = paymentMethod.DynamicProperty<string>().MerchantId;
		    string secretKey = paymentMethod.DynamicProperty<string>().SecretKey;
			string subId = paymentMethod.DynamicProperty<string>().SubId;
		    string language = paymentMethod.DynamicProperty<string>().Language;
		    string successUrl = paymentMethod.DynamicProperty<string>().SuccessUrl;
		    string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;
		    string errorUrl = paymentMethod.DynamicProperty<string>().ErrorUrl;


		    const string paymentType = "ideal";
			const string description = "Order total";
			string purchaseId = paymentRequest.Payment.ReferenceId;
			string amountInCents = paymentRequest.Payment.Amount.ToCents().ToString();
			string currencyIsoCode = paymentRequest.Amount.CurrencyIsoCode;
			string orderId = paymentRequest.Payment.ReferenceId;
			string validUntil = string.Format("{0:s}0Z", paymentRequest.Payment.Created.AddMinutes(20));

			var parametersToReturn = new Dictionary<string, string>();

			parametersToReturn["subId"] = subId;
			parametersToReturn["merchantId"] = merchantId;
			parametersToReturn["purchaseID"] = purchaseId;
			parametersToReturn["amount"] = amountInCents;
			parametersToReturn["currency"] = currencyIsoCode;
			parametersToReturn["language"] = language;
			parametersToReturn["orderId"] = orderId;
			parametersToReturn["itemNumber1"] = orderId;
		    parametersToReturn["itemDescription1"] = description;
			parametersToReturn["itemQuantity1"] = "1";
			parametersToReturn["itemPrice1"] = amountInCents;
		    parametersToReturn["paymentType"] = paymentType;
			parametersToReturn["validUntil"] = validUntil;
			parametersToReturn["hash"] = GetSha1KeyForPaymentRequest(amountInCents, purchaseId, paymentType, validUntil, orderId, description, secretKey, merchantId, subId);
		    
			parametersToReturn["urlSuccess"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(successUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			parametersToReturn["urlCancel"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();
			parametersToReturn["urlError"] = new Uri(_absoluteUrlService.GetAbsoluteUrl(errorUrl)).AddOrderGuidParameter(paymentRequest.Payment.PurchaseOrder).ToString();

		    return parametersToReturn;
	    }

	    protected virtual string GetSha1KeyForPaymentRequest(string amountInCents, string purchaseId, string paymentType, string validUntil, string orderId, string description, string secretKey, string merchantId, string subId)
	    {
			var hashComputer = new IdealSha1Computer();

		    return hashComputer.GetSha1Key(amountInCents, purchaseId, paymentType, validUntil, orderId, description, secretKey, merchantId, subId);
	    }

	    protected virtual void AddParameters(StringBuilder page, PaymentRequest paymentRequest, IDictionary<string, string> parametersToAdd)
	    {
		    bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;

			page.Append(string.Format(@"<form method=""post"" action=""{0}"">", testMode
							? "https://idealtest.secure-ing.com/ideal/mpiPayInitIng.do"
							: "https://ideal.secure-ing.com/ideal/mpiPayInitIng.do"));

		    foreach (var parameterToAdd in parametersToAdd)
		    {
			    AddHiddenField(page,parameterToAdd.Key,parameterToAdd.Value);
		    }

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");

			page.Append("</form>");

	    }

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
	        var paramters = GetParameters(paymentRequest);
			AddParameters(page,paymentRequest,paramters);
        }
    }
}

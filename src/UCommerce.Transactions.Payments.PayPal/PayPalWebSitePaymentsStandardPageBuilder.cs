using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure.Components.Windsor;
using Ucommerce.Transactions.Payments.Configuration;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.PayPal
{
    public class PayPalWebSitePaymentsStandardPageBuilder : AbstractPageBuilder
    {
        [Mandatory] public IAbsoluteUrlService AbsoluteUrlService { get; set; }
        [Mandatory] public ICallbackUrl CallbackUrl { get; set; }

        private const string URL_SANDBOX = "https://www.sandbox.paypal.com/cgi-bin/webscr";
        private const string URL_PRODUCTION = "https://www.paypal.com/cgi-bin/webscr";

        public string GetPostUrl(PaymentMethod paymentMethod)
        {
            return paymentMethod.DynamicProperty<bool>().Sandbox
                ? URL_SANDBOX
                : URL_PRODUCTION;
        }

        protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append("<title>Paypal</title>");
            page.Append(
                @"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
            if (!Debug)
                page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
        }

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append(@"<form method=""post"" action=""" + GetPostUrl(paymentRequest.PaymentMethod) + @""">");

            // All required fields
            IDictionary<string, string> dict = GetParameters(paymentRequest);

            if (Debug)
                AddSubmitButton(page, "ac", "Post it");

            if (paymentRequest.PaymentMethod.DynamicProperty<bool>().UseEncryption)
            {
                var encrypter = new ButtonEncrypter(paymentRequest.PaymentMethod);
                AddHiddenField(page, "cmd", "_s-xclick");
                AddHiddenField(page, "encrypted", encrypter.SignAndEncrypt(dict));
            }
            else
            {
                foreach (var pair in dict)
                {
                    AddHiddenField(page, pair.Key, pair.Value);
                }
            }

            page.Append("</form>");
        }

        protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
        {
            var currency = paymentRequest.Amount.CurrencyIsoCode;
            // For dynamics, we have to call the extension method as a normal method.
            ReturnMethod rm =
                EnumExtensions.ParseReturnMethodThrowExceptionOnFailure(paymentRequest.PaymentMethod
                    .DynamicProperty<string>().ReturnMethod);
            PaymentAction pa =
                EnumExtensions.ParsePaymentActionThrowExceptionOnFailure(paymentRequest.PaymentMethod
                    .DynamicProperty<string>().PaymentAction);

            var dict = new Dictionary<string, string>
            {
                {"upload", "1"},
                {"cmd", "_cart"},
                {"business", paymentRequest.PaymentMethod.DynamicProperty<string>().Business},
                {
                    "return",
                    new Uri(AbsoluteUrlService.GetAbsoluteUrl(paymentRequest.PaymentMethod.DynamicProperty<string>()
                            .Return))
                        .AddOrderGuidParameter(
                            paymentRequest.Payment.PurchaseOrder).ToString()
                },
                {"rm", ((int) rm).ToString()},
                // Vendor code to identify Ucommerce
                {"bn", "Ucommerce_SP"},
                {"currency_code", currency},
                {"no_shipping", "1"},
                {"paymentaction", pa.ToString().ToLower()},
                {
                    "cancel_return",
                    AbsoluteUrlService.GetAbsoluteUrl(paymentRequest.PaymentMethod.DynamicProperty<string>()
                        .CancelReturn)
                },
                {
                    "notify_url",
                    CallbackUrl.GetCallbackUrl(paymentRequest.PaymentMethod.DynamicProperty<string>().NotifyUrl,
                        paymentRequest.Payment)
                },
                {"invoice", paymentRequest.Payment.ReferenceId},
                {
                    "item_name_1",
                    string.Join(", ", paymentRequest.Payment.PurchaseOrder.OrderLines.Select(x => x.ProductName))
                },
                {"amount_1", paymentRequest.Payment.Amount.ToInvariantString()},
                {"quantity_1", "1"}
            };

            return dict;
        }
    }
}
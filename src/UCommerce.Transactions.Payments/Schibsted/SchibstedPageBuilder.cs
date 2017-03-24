using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Infrastructure.Configuration;
using UCommerce.Infrastructure.Globalization;
using UCommerce.Infrastructure.Logging;
using UCommerce.Transactions.Payments.Common;
using UCommerce.Transactions.Payments.Configuration;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.Schibsted
{
    public class SchibstedPageBuilder : AbstractPageBuilder
    {
	    private readonly IAbsoluteUrlService _absoluteUrlService;

	    private readonly ICallbackUrl _callbackUrl;
	    protected SchibstedSha256Computer Sha256Computer { get; set; }
        protected ILoggingService LoggingService { get; set; }
        protected CustomGlobalization LocalizationContext { get; set; }

        public SchibstedPageBuilder(CommerceConfigurationProvider configurationProvider, SchibstedSha256Computer sha256Computer, ILoggingService loggingService, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl)
        {
	        _absoluteUrlService = absoluteUrlService;
	        _callbackUrl = callbackUrl;
	        Sha256Computer = sha256Computer;
            LoggingService = loggingService;
            LocalizationContext = new CustomGlobalization(configurationProvider);
        }

        protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
        {
            page.Append("<title>Schibsted</title>");
            page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
            if (!Debug)
                page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
        }

        protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
        {
			var paymentMethod = paymentRequest.PaymentMethod;
			string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
			string clientId = paymentMethod.DynamicProperty<string>().ClientId;
			string callBackUrl = paymentMethod.DynamicProperty<string>().CallbackUrl;
			string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;
			string paymentOptions = paymentMethod.DynamicProperty<string>().PaymentOptions;
			bool autoCapture = paymentMethod.DynamicProperty<bool>().AutoCapture;

	        // Creating product order items
	        var orderItems = BuildOrderItemsList(paymentRequest);

	        try
            {
                // Getting server token
				var serverToken = GetSchibstedUtil(paymentMethod).GetServerToken(clientId, clientSecret);

                // Trying to create a paylink
                var paylink = GetPaylink(serverToken, orderItems, paymentRequest.Payment);

                // Saving paylink url in session
                paymentRequest.Payment["paylinkUrl"] = paylink.Data.ShortUrl;
                paymentRequest.Payment.Save();

                // Redirecting to paylink url
                if (Debug)
                {
                    // Show debug information
                    var clientReference = Sha256Computer.ComputeHash(
                        paymentRequest.Payment.ReferenceId +
                        orderItems.Count(x => x.Type == 100) +
                        paymentRequest.Payment.Amount, clientSecret,
                        true);

                    page.Append("<b>oauth_token:</b> " + serverToken.AccessToken + "<br />");
                    page.Append("<b>title:</b> " + paymentRequest.Payment.ReferenceId + "<br />");
                    page.Append("<b>purchaseFlow:</b> " + (autoCapture ? "DIRECT" : "AUTHORIZE") + "<br />");
                    page.Append("<b>paymentOptions:</b> " + paymentOptions + "<br />");
                    page.Append("<b>redirectUri:</b> " + _callbackUrl.GetCallbackUrl(callBackUrl,paymentRequest.Payment) + "<br />");
                    page.Append("<b>cancelUri:</b> " + _absoluteUrlService.GetAbsoluteUrl(cancelUrl)  + "<br />");
                    page.Append("<b>clientReference:</b> " + clientReference + "<br />");
                    page.Append("<b>items (json):</b><br />");
                    page.Append("<code>");
                    page.Append(GetSchibstedUtil(paymentMethod).GetJsonStringFromOrderItems(orderItems));
                    page.Append("</code><br /><br />");
                    page.Append("<a href=\"" + paylink.Data.ShortUrl + "\">Proceed to payment</a>");
                }
                else
                {
                    HttpContext.Current.Response.Redirect(paylink.Data.ShortUrl);
                }
            }
            catch (WebException ex)
            {
                // Something happened, log api error message
                LogWebException(ex);
                throw new Exception("API Error, see log for details");
            }
        }

	    protected virtual List<OrderItem> BuildOrderItemsList(PaymentRequest paymentRequest)
	    {
		    string includeOrderProperties = paymentRequest.PaymentMethod.DynamicProperty<string>().IncludeOrderProperties;
			// Creating product order items
			var orderItems = new List<OrderItem>();

			var orderProperties = includeOrderProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var orderline in paymentRequest.PurchaseOrder.OrderLines)
			{
				var order = new OrderItem
				{
					Description = orderline.ProductName,
					Currency = orderline.PurchaseOrder.BillingCurrency.ISOCode,
					Price = orderline.Total.ToCents() / orderline.Quantity,
					Vat = Convert.ToInt32(orderline.VATRate * 10000),
					Quantity = orderline.Quantity,
					Type = 100, // regular product type
					Properties = new Dictionary<string, object>()
				};

				foreach (var property in orderProperties)
				{
					order.Properties.Add(property, orderline[property]);
				}

				orderItems.Add(order);
			}

			if (paymentRequest.Payment.FeeTotal > 0)
			{
				// Creating payment method fee item
				orderItems.Add(new OrderItem
				{
					ClientItemReference = "paymentFee",
					Description =
						paymentRequest.PaymentMethod.GetDescription(paymentRequest.PurchaseOrder.CultureCode)
							.DisplayName,
					Currency = paymentRequest.PurchaseOrder.BillingCurrency.ISOCode,
					Price = paymentRequest.Payment.FeeTotal.ToCents(),
					Vat = Convert.ToInt32(paymentRequest.Payment.FeePercentage * 100),
					Quantity = 1,
					Type = 201 // payment method fee
				});
			}

			// Creating shipment fee item
			foreach (var shipment in paymentRequest.PurchaseOrder.Shipments.Where(x => x.ShipmentTotal > 0))
			{
				orderItems.Add(new OrderItem
				{
					ClientItemReference = shipment.ShipmentName,
					Description = shipment.ShippingMethod.GetDescription(paymentRequest.PurchaseOrder.CultureCode).DisplayName,
					Currency = shipment.PurchaseOrder.BillingCurrency.ISOCode,
					Price = shipment.ShipmentTotal.ToCents(),
					Vat = Convert.ToInt32(shipment.TaxRate * 10000),
					Quantity = 1,
					Type = 201 // shipping fee: 204, doesn't work
				});
			}

		    return orderItems;
	    }

	    private SppContainer<PayLinkData> GetPaylink(OAuthToken serverToken, IEnumerable<OrderItem> orderItems, Payment payment)
	    {
		    var paymentMethod = payment.PaymentMethod;
		    string clientSecret = paymentMethod.DynamicProperty<string>().ClientSecret;
		    string callBackUrl = paymentMethod.DynamicProperty<string>().CallbackUrl;
			string cancelUrl = paymentMethod.DynamicProperty<string>().CancelUrl;
		    string title = paymentMethod.DynamicProperty<string>().Title;
			string paymentOptions = paymentMethod.DynamicProperty<string>().PaymentOptions;
			bool autoCapture = paymentMethod.DynamicProperty<bool>().AutoCapture;
			
		    var items = orderItems.ToList();
			var clientReference = Sha256Computer.ComputeHash(payment.ReferenceId + items.Count(x => x.Type == 100) + payment.Amount, clientSecret, true);

            var itemsJson = GetSchibstedUtil(paymentMethod).GetJsonStringFromOrderItems(items);

            var postValues = new Dictionary<string, string>
            {
                {"oauth_token", serverToken.AccessToken},
                {"title", title},
                {"purchaseFlow", autoCapture ? "DIRECT" : "AUTHORIZE"},
                {"paymentOptions", paymentOptions},
                {"redirectUri", _callbackUrl.GetCallbackUrl(callBackUrl,payment) },
                {"cancelUri", _absoluteUrlService.GetAbsoluteUrl(cancelUrl) },
                {"clientReference", clientReference},
                {"items", HttpUtility.UrlEncode(itemsJson)}
            };

		    var schibstedUtil = GetSchibstedUtil(payment.PaymentMethod);
			return schibstedUtil.SchibstedApiPost<PayLinkData>("/paylink", postValues);
        }

	    protected SchibstedUtil GetSchibstedUtil(PaymentMethod paymentMethod)
	    {
		    bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			return new SchibstedUtil(testMode);
	    }

        private void LogWebException(WebException ex)
        {
            // Logging JSON error response
            var sr = new StreamReader(ex.Response.GetResponseStream());
            LoggingService.Log<SchibstedPaymentMethodService>("JSON Error Response: " + sr.ReadToEnd());
        }
    }
}
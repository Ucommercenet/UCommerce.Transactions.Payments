using System;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Stripe;
using Stripe.Issuing;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Pipelines.Transactions.Baskets.Basket;
using Ucommerce.Web;
using File = System.IO.File;

namespace Ucommerce.Transactions.Payments.StripeNet
{
	public class StripePageBuilder : AbstractPageBuilder
    {
	    private readonly ICallbackUrl _callbackUrl;
		private readonly string PaymentIntentKey = "paymentIntent";

	    public StripePageBuilder(ICallbackUrl callbackUrl)
	    {
		    _callbackUrl = callbackUrl;
	    }

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			// create client
			string apiKey = paymentRequest.PaymentMethod.DynamicProperty<string>().PublicKey;
			string apiSecret = paymentRequest.PaymentMethod.DynamicProperty<string>().SecretKey;
			string acceptUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().AcceptUrl;

			var client = new StripeClient(apiSecret);
			var paymentIntentService = new PaymentIntentService(client);

			var createIntent = new PaymentIntentCreateOptions() {
				Currency = paymentRequest.Amount.CurrencyIsoCode,
				Amount = Convert.ToInt64(paymentRequest.Amount.Value * 100)
			};

			var paymentIntent = paymentIntentService.Create(createIntent);
			string paymentFormTemplate = paymentRequest.PaymentMethod.DynamicProperty<string>().PaymentFormTemplate;
			var allLines = File.ReadAllLines(HttpContext.Current.Server.MapPath(paymentFormTemplate));
			foreach (var line in allLines)
			{
				page.AppendLine(line);
			}

			var billingDetails = new ChargeBillingDetails() {
				Name = $"{paymentRequest.PurchaseOrder.BillingAddress.FirstName} {paymentRequest.PurchaseOrder.BillingAddress.LastName}",
				Address = new Stripe.Address() {
					Line1 = paymentRequest.PurchaseOrder.BillingAddress.Line1,
					Line2 = paymentRequest.PurchaseOrder.BillingAddress.Line2,
					City = paymentRequest.PurchaseOrder.BillingAddress.City,
					State = paymentRequest.PurchaseOrder.BillingAddress.State,
					PostalCode = paymentRequest.PurchaseOrder.BillingAddress.PostalCode,
					Country = paymentRequest.PurchaseOrder.BillingAddress.Country.TwoLetterISORegionName
				}
			};

			page.Replace("##STRIPE:PUBLICKEY##", apiKey);
			page.Replace("##STRIPE:PAYMENTINTENT##", JsonConvert.SerializeObject(paymentIntent));
			page.Replace("##STRIPE:BILLINGDETAILS##", JsonConvert.SerializeObject(billingDetails));
			page.Replace("##PROCESSURL##", $"/{paymentRequest.PaymentMethod.PaymentMethodId}/{paymentRequest.Payment["paymentGuid"]}/PaymentProcessor.axd");
			page.Replace("##ACCEPTURL##", acceptUrl);

			PaymentProperty paymentIntentProp;
			if (paymentRequest.Payment.PaymentProperties.Any(p => p.Key == PaymentIntentKey)) {
				var allProps = paymentRequest.Payment.PaymentProperties.Where(p => p.Key == PaymentIntentKey).ToList();
				foreach (var prop in allProps) {
					paymentRequest.Payment.PaymentProperties.Remove(prop);
                }
				paymentRequest.Payment.Save();
			}

			paymentIntentProp = new PaymentProperty() {
				Guid = Guid.NewGuid(),
				Key = PaymentIntentKey,
				Value = paymentIntent.Id
			};
			paymentRequest.Payment.AddPaymentProperty(paymentIntentProp);
			paymentRequest.Payment.Save();
		}
    }
}

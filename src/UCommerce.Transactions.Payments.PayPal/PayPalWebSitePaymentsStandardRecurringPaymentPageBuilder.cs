using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.Extensions;

namespace Ucommerce.Transactions.Payments.PayPal
{
	/// <summary>
	/// Page builder for generating encrypted PayPal buttons for subscriptions.
	/// </summary>
	public class PayPalWebSitePaymentsStandardRecurringPaymentPageBuilder : PayPalWebSitePaymentsStandardPageBuilder
	{
		public PayPalWebSitePaymentsStandardRecurringPaymentPageBuilder() {}

		protected override IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			var dict = base.GetParameters(paymentRequest);
			dict["cmd"] = "_xclick-subscriptions";
			
			// Subscription name
			dict.Add("item_name", string.Join(", ", paymentRequest.Payment.PurchaseOrder.OrderLines.Select(x => x.ProductName)));

			// Subscription amount
			dict.Add("a3", paymentRequest.Payment.Amount.ToInvariantString());

			// Add all custom properties from the payment to the form.
			// This includes variable information about recurrence.
			foreach (var property in paymentRequest.Payment.PaymentProperties)
				dict[property.Key] = property.Value;

			// Should PayPal retry failed recurring payments
			dict.Add("sra", "1");

			// No note for customers
			dict.Add("no_note", "1");

			// Can user manage subscription (0/1)
			dict.Add("usr_manage", "0");

			return dict;
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
	}
}

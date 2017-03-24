using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.Authorizedotnet
{
	public class AuthorizedotnetPageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;

		public AuthorizedotnetPageBuilder(ICallbackUrl callbackUrl)
		{
			_callbackUrl = callbackUrl;
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>Authorize.net</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if (!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			// Configuration values
			bool sandboxMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().SandboxMode;
			bool testMode = paymentRequest.PaymentMethod.DynamicProperty<bool>().TestMode;
			bool instantAcquire = paymentRequest.PaymentMethod.DynamicProperty<bool>().InstantAcquire;
			bool itemizeReceipt = paymentRequest.PaymentMethod.DynamicProperty<bool>().ItemizeReceipt;
			string apiLogin = paymentRequest.PaymentMethod.DynamicProperty<string>().ApiLogin;
			string transactionKey = paymentRequest.PaymentMethod.DynamicProperty<string>().TransactionKey;
			string callbackUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string payType = paymentRequest.PaymentMethod.DynamicProperty<string>().PayType;
			string logoUrl = paymentRequest.PaymentMethod.DynamicProperty<string>().LogoUrl;

			page.Append(string.Format(@"<form method=""post"" action=""{0}"">", sandboxMode ? "https://test.authorize.net/gateway/transact.dll" : "https://secure.authorize.net/gateway/transact.dll"));

			AddHiddenField(page, "x_login", apiLogin);
			AddHiddenField(page, "x_type", instantAcquire ? "AUTH_CAPTURE" : "AUTH_ONLY");
			AddHiddenField(page, "x_show_form", "PAYMENT_FORM");
			AddHiddenField(page, "x_relay_response", "true");
			AddHiddenField(page, "x_relay_url", _callbackUrl.GetCallbackUrl(callbackUrl, paymentRequest.Payment));
			AddHiddenField(page, "x_delim_data", "FALSE");
			AddHiddenField(page, "x_version", "3.1");
			AddHiddenField(page, "x_method", payType);
			AddHiddenField(page, "x_invoice_num", paymentRequest.Payment.ReferenceId);


			OrderAddress billingAddress = paymentRequest.Payment.PurchaseOrder.BillingAddress;
			AddHiddenField(page, "x_first_name", billingAddress.FirstName);
			AddHiddenField(page, "x_last_name", billingAddress.LastName);
			AddHiddenField(page, "x_address", billingAddress.Line1 + (string.IsNullOrEmpty(billingAddress.Line2) ? "" : (", " + billingAddress.Line2)));
			AddHiddenField(page, "x_city", billingAddress.City);
			AddHiddenField(page, "x_zip", billingAddress.PostalCode);
			AddHiddenField(page, "x_country", billingAddress.Country.Name);
			AddHiddenField(page, "x_email", billingAddress.EmailAddress);
			AddHiddenField(page, "x_phone", billingAddress.PhoneNumber);
			AddHiddenField(page, "x_company", billingAddress.CompanyName);

			if (!string.IsNullOrEmpty(billingAddress.State))
				AddHiddenField(page, "x_state", billingAddress.State);

			if (itemizeReceipt)
			{
				foreach (OrderLine line in paymentRequest.PurchaseOrder.OrderLines)
				{
					string fullSku = string.Format("{0}{1}", line.Sku, !string.IsNullOrEmpty(line.VariantSku) ? "-" + line.VariantSku : "");
					AddHiddenField(page, "x_line_item", string.Format("{0}<|>{1}<|>{2}<|>{3}<|>{4}<|>N",
						HttpUtility.HtmlEncode(MaxThirtyChars(fullSku)),
						HttpUtility.HtmlEncode(MaxThirtyChars(fullSku)),
						HttpUtility.HtmlEncode(MaxThirtyChars(line.ProductName)),
						line.Quantity,
						(line.Price - line.UnitDiscount).ToInvariantString()));
				}
			}

			AddHiddenField(page, "x_header_html_payment_form", "<style type='text/css'>#imgMerchantLogo{}</style>");
			
			if (!string.IsNullOrEmpty(logoUrl))
				AddHiddenField(page, "x_logo_url", logoUrl);

			var amount = paymentRequest.Payment.Amount.ToInvariantString();
			AddHiddenField(page, "x_amount", amount);

			AddHiddenField(page, "x_duplicate_window", 28800.ToString());
			AddHiddenField(page, "x_customer_ip", HttpContext.Current.Request.UserHostAddress);

			// To get a transactionsid for authorizations Sandbox mode requires testmode not to be enabled
			if (testMode && !sandboxMode)
				AddHiddenField(page, "x_test_request", "TRUE");

			var hashComputer = new AuthorizedotnetMd5Computer();
			string sequence = paymentRequest.Payment.ReferenceId;
			AddHiddenField(page, "x_fp_sequence", sequence);

			TimeSpan timeSinceCreate = (paymentRequest.Payment.Created.ToUniversalTime() - new DateTime(1970, 1, 1));
			string timestamp = ((int)timeSinceCreate.TotalSeconds).ToString();

			AddHiddenField(page, "x_fp_timestamp", timestamp);
			AddHiddenField(page, "x_fp_hash", hashComputer.GetPreMd5Key(transactionKey, apiLogin, sequence, timestamp, amount));

			if (Debug)
				AddSubmitButton(page, "ac", "Post it");

			page.Append("</form>");
		}

		private string MaxThirtyChars(string originalString)
		{
			return ((originalString.Length > 30) ? originalString.Substring(0, 30) : originalString);
		}
	}
}

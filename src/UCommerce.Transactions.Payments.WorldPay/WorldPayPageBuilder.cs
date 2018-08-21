using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;
using UCommerce.Web;

namespace UCommerce.Transactions.Payments.WorldPay
{
	public class WorldPayPageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;
		public WorldPayMd5Computer Md5Computer { get; set; }

		private const string URL_SANDBOX = "https://secure-test.worldpay.com/wcc/purchase";
		private const string URL_PRODUCTION = "https://secure.worldpay.com/wcc/purchase";

		public string GetPostUrl(PaymentMethod paymentMethod)
		{
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			return testMode ? URL_SANDBOX : URL_PRODUCTION;
		}

		public WorldPayPageBuilder(ICallbackUrl callbackUrl, WorldPayMd5Computer md5Computer)
		{
			_callbackUrl = callbackUrl;
			Md5Computer = md5Computer;
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>WorldPay</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if (!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append(@"<form method=""post"" action=""" + GetPostUrl(paymentRequest.PaymentMethod) + @""">");

			var parameters = GetParameters(paymentRequest);

			foreach (var parameter in parameters)
			{
				AddHiddenField(page, parameter.Key, parameter.Value);
			}

			if(Debug)
				AddSubmitButton(page, "ac", "Post it");

			page.Append("</form>");
		}

		protected virtual IDictionary<string, string> GetParameters(PaymentRequest paymentRequest)
		{
			PaymentMethod paymentMethod = paymentRequest.PaymentMethod;
			Payment payment = paymentRequest.Payment;

			var dict = new Dictionary<string, string>();
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;
			bool instantCapture = paymentMethod.DynamicProperty<bool>().InstantCapture;
			string callback = paymentMethod.DynamicProperty<string>().Callback;
			string instId = paymentMethod.DynamicProperty<string>().InstId;
			string key = paymentMethod.DynamicProperty<string>().Key;
			string signature = paymentMethod.DynamicProperty<string>().Signature;

			// All required fields
			var amount = paymentRequest.Payment.Amount.ToString("0.00", CultureInfo.InvariantCulture);
			var currency = paymentRequest.Amount.Currency.ISOCode;

			dict.Add("testMode", (testMode ? 100 : 0).ToString());

			// Specifies the authorisation mode used. The values are "A" for a full auth, or "E" for a pre-auth.
			if (instantCapture)
				dict.Add("authMode", "A");
			else
				dict.Add("authMode", "E");

			var hash = Md5Computer.GetHash(payment.Amount, payment.ReferenceId, payment.PurchaseOrder.BillingCurrency.ISOCode, key);
			var cartId = paymentRequest.Payment.ReferenceId;
			var callbackUrl = _callbackUrl.GetCallbackUrl(callback, paymentRequest.Payment);

			dict.Add("MC_hash", hash);
			dict.Add("instId", instId);

			dict.Add("cartId", cartId);
			dict.Add("amount", amount);
			dict.Add("currency", currency);

			// instId:amount:currency:cartId:MC_callback

			IList<string> signatureList = new List<string>();
			signatureList.Add(instId);
			signatureList.Add(amount);
			signatureList.Add(currency);
			signatureList.Add(cartId);
			signatureList.Add(callbackUrl);

			var signatureHash = Md5Computer.GetSignatureHash(signatureList, signature);

			dict.Add("signature", signatureHash);
			dict.Add("MC_callback", callbackUrl);

			return dict;
		}
	}
}
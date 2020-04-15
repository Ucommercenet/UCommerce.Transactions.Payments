using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure.Globalization;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.Payer
{
	/// <summary>
	/// Builds a Payer redirect page.
	/// </summary>
	public class PayerPageBuilder : AbstractPageBuilder
	{
		private readonly ICallbackUrl _callbackUrl;
		private readonly IAbsoluteUrlService _absoluteUrlService;
		private ILocalizationContext LocalizationContext { get; set; }
		private PayerMd5Computer Md5Computer { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PayerPageBuilder"/> class.
		/// </summary>
		public PayerPageBuilder(ILocalizationContext localizationContext, PayerMd5Computer md5Computer, ICallbackUrl callbackUrl, IAbsoluteUrlService absoluteUrlService)
		{
			_callbackUrl = callbackUrl;
			_absoluteUrlService = absoluteUrlService;
			LocalizationContext = localizationContext;
			Md5Computer = md5Computer;
		}

		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>Payer</title>");
			page.Append(@"<script type=""text/javascript"" src=""//ajax.googleapis.com/ajax/libs/jquery/1.4.2/jquery.min.js""></script>");
			if(!Debug)
				page.Append(@"<script type=""text/javascript"">$(function(){ $('form').submit();});</script>");
		}

		/// <summary>
		/// Builds the form.
		/// </summary>
		/// <param name="page">The <see cref="StringBuilder" />.</param>
		/// <param name="paymentRequest">The payment request.</param>
		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			string agentId = paymentRequest.PaymentMethod.DynamicProperty<string>().AgentId;
			string key1 = paymentRequest.PaymentMethod.DynamicProperty<string>().Key1;
			string key2 = paymentRequest.PaymentMethod.DynamicProperty<string>().Key2;

			page.Append(@"<form id=""Payer"" name=""Payer"" method=""post"" action=""https://secure.pay-read.se/PostAPI_V1/InitPayFlow"">");

			AddHiddenField(page, "payread_agentid", agentId);
			AddHiddenField(page, "payread_xml_writer", "payread_asp_0.1");

			string prpGeneratePurchaseXml = GeneratePurchaseXml(paymentRequest.Payment);

			byte[] bSourceData = Encoding.UTF8.GetBytes(prpGeneratePurchaseXml);
			var ec = Encoding.GetEncoding("ISO-8859-1");

			byte[] byteTextB = Encoding.Convert(Encoding.UTF8, ec, bSourceData);
			
			var s = Convert.ToBase64String(byteTextB);

			AddHiddenField(page, "payread_data", s);

			string preMd5Key = Md5Computer.GetMd5Key(s, key1, key2);

			AddHiddenField(page, "payread_checksum", preMd5Key);

			if(Debug)
				AddSubmitButton(page, "ac", "Post it");

			page.Append("</form>");
		}

		private string GeneratePurchaseXml(Payment payment)
		{
			string agentId = payment.PaymentMethod.DynamicProperty<string>().AgentId;
			string callbackUrl = payment.PaymentMethod.DynamicProperty<string>().CallbackUrl;
			string acceptUrl = payment.PaymentMethod.DynamicProperty<string>().AcceptUrl;
			string cancelUrl = payment.PaymentMethod.DynamicProperty<string>().CancelUrl;
			bool testMode = payment.PaymentMethod.DynamicProperty<bool>().TestMode;

			var xmlData = new StringBuilder();

			//Header
			xmlData.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
			xmlData.AppendLine("<payread_post_api_0_2 xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"payread_post_api_0_2.xsd\">");

			//Seller details
			xmlData.AppendLine(@" <seller_details>");
			xmlData.AppendFormat(@"<agent_id>{0}</agent_id>", agentId);
			xmlData.AppendLine();
			xmlData.AppendLine("</seller_details>");

			var purchaseOrder = payment.PurchaseOrder;
			var billingAddress = purchaseOrder.GetBillingAddress();
			//Buyer details
			xmlData.AppendLine("<buyer_details>");
			xmlData.AppendLine(string.Format("<first_name>{0}</first_name>", billingAddress.FirstName));
			xmlData.AppendLine(string.Format("<last_name>{0}</last_name>", billingAddress.LastName));
			xmlData.AppendLine(string.Format("<address_line_1>{0}</address_line_1>", billingAddress.Line1));
			xmlData.AppendLine(string.Format("<address_line_2>{0}</address_line_2>", billingAddress.Line2));
			xmlData.AppendLine(string.Format("<postal_code>{0}</postal_code>", billingAddress.PostalCode));
			xmlData.AppendLine(string.Format("<city>{0}</city>", billingAddress.City));
			xmlData.AppendLine(string.Format("<phone_home>{0}</phone_home>", billingAddress.PhoneNumber));
			xmlData.AppendLine(string.Format("<phone_work>{0}</phone_work>", billingAddress.PhoneNumber));
			xmlData.AppendLine(string.Format("<phone_mobile>{0}</phone_mobile>", billingAddress.MobilePhoneNumber));
			xmlData.AppendLine(string.Format("<email>{0}</email>", billingAddress.EmailAddress));
			xmlData.AppendLine("</buyer_details>");

			//Purchase start
			xmlData.AppendLine("<purchase>");
			xmlData.AppendFormat("<currency>{0}</currency>", purchaseOrder.BillingCurrency.ISOCode);
			xmlData.AppendFormat("<reference_id>{0}</reference_id>", payment.ReferenceId);
			xmlData.AppendLine();
			xmlData.AppendLine("<purchase_list>");

			xmlData.AppendLine("<freeform_purchase>");
			xmlData.AppendLine("<line_number>0</line_number>");
			xmlData.AppendLine("<description>Sum</description>");
			xmlData.AppendLine(string.Format("<price_including_vat>{0}</price_including_vat>", payment.Amount.ToString("0.##", CultureInfo.InvariantCulture)));

			decimal vatRate = purchaseOrder.OrderLines.First().VATRate * 100;

			xmlData.AppendFormat("<vat_percentage>{0}</vat_percentage>", vatRate.ToString("#.00", CultureInfo.InvariantCulture));
			xmlData.AppendLine("<quantity>1</quantity>");
			xmlData.AppendLine("</freeform_purchase>");

			//Purchase end
		 	xmlData.AppendLine("</purchase_list>");
			xmlData.AppendLine("</purchase>");

			//Processing Control
			xmlData.AppendLine("<processing_control>");
			
			callbackUrl = _callbackUrl.GetCallbackUrl(callbackUrl, payment);

			xmlData.AppendLine(string.Format("<success_redirect_url>{0}</success_redirect_url>", 
				new Uri(_absoluteUrlService.GetAbsoluteUrl(acceptUrl)).AddOrderGuidParameter(purchaseOrder)));

			xmlData.AppendLine(string.Format("<redirect_back_to_shop_url>{0}</redirect_back_to_shop_url>",
				new Uri(_absoluteUrlService.GetAbsoluteUrl(cancelUrl)).AddOrderGuidParameter(purchaseOrder)));

			xmlData.AppendLine(string.Format("<authorize_notification_url>{0}</authorize_notification_url>", callbackUrl));
			
			xmlData.AppendLine(string.Format("<settle_notification_url>{0}</settle_notification_url>", callbackUrl));
			
			xmlData.AppendLine("</processing_control>");

			//Database overrides start
			xmlData.AppendLine("<database_overrides>");

			//Payment flags
			xmlData.AppendLine("<accepted_payment_methods>");
			xmlData.AppendLine("<payment_method>card</payment_method>");
			xmlData.AppendLine("</accepted_payment_methods>");

			if (Debug)
				xmlData.AppendLine("<debug_mode>verbose</debug_mode>");
			else
				xmlData.AppendLine("<debug_mode>silent</debug_mode>");

			if (testMode)
				xmlData.AppendLine("<test_mode>true</test_mode>");

			xmlData.AppendFormat("<language>{0}</language>", LocalizationContext.CurrentCulture.TwoLetterISOLanguageName);
			xmlData.AppendLine("</database_overrides>");
			//Database overrides end

			//Footer
			xmlData.AppendLine("</payread_post_api_0_2>");

			return xmlData.ToString();
		}
	}
}
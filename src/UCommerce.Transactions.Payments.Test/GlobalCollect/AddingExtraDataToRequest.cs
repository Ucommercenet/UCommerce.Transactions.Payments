using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UCommerce.Transactions.Payments.GlobalCollect.Api;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.Test.GlobalCollect
{
	[TestFixture]
	public class AddingExtraDataToRequest
	{
		[Test]
		public void Can_Add_Extra_Data_To_Payment()
		{
			// Arrange
			var payment = new ApiPayment {PaymentProductId = 123, LanguageCode = "da", MerchantReference = "JustMyLuck"};

			// Act
			payment.AddExtraData("uCommerce", "ECOM");
			var text = ConvertApiDataPartToString(payment);

			// Assert
			Assert.IsTrue(text.Contains("<ECOM>uCommerce</ECOM>"));
		}

		[Test]
		public void Can_Add_Extra_Data_To_Order()
		{
			// Arrange
			var order = new ApiOrder { OrderId = 123, LanguageCode = "da", MerchantReference = "JustMyLuck" };

			// Act
			order.AddExtraData("uCommerce", "ECOM");
			var text = ConvertApiDataPartToString(order);

			// Assert
			Assert.IsTrue(text.Contains("<ECOM>uCommerce</ECOM>"));
		}

		private static string ConvertApiDataPartToString(IApiDataPart part)
		{
			var sb = new StringBuilder();
			part.AddToStringBuilder(sb);
			return sb.ToString();
		}

	}
}

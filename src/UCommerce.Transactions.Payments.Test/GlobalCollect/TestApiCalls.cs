using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Ucommerce.Transactions.Payments.GlobalCollect;
using Ucommerce.Transactions.Payments.GlobalCollect.Api;

namespace Ucommerce.Transactions.Payments.Test.GlobalCollect
{
	[TestFixture]
	public class TestApiCalls
	{
		[Test, Ignore("")]
		public void TestConnection()
		{
			// Arrange
			string requestText =
				@"<XML><REQUEST><ACTION>TEST_CONNECTION</ACTION><META><MERCHANTID>1</MERCHANTID><IPADDRESS>83.151.151.142</IPADDRESS><VERSION>1.0</VERSION></META></REQUEST></XML>";
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(requestText);

			// Assert
			AssertResponseOk(responseText);
		}

	    [Test, Ignore("")]
	    public void GetPaymentProducts()
		{
			// Arrange
			var getPaymentProducts = new GetPaymentProducts
			{
				Meta = { MerchantId = 7454 },
				General = {LanguageCode = "en", CountryCode = "GB", CurrencyCode = "EUR"}
			};

			var text = getPaymentProducts.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);

			// Assert
			AssertResponseOk(responseText);
			var response = new GetPaymentProducts();
			response.FromModifiedXml(new ModifiedXmlDocument(responseText), string.Empty);
			Assert.AreEqual("OK", response.Response.Result);
		}

		[Test, Ignore("")]
		public void InsertOrderWithPayment()
		{
			// Arrange
			var request = new InsertOrderWithPayment
			{
				Meta = { MerchantId = 7454 },
				Order = { Amount = 100, CurrencyCode = "EUR", CountryCode = "DK", LanguageCode = "da", MerchantReference = "MyReference12345"},
				Payment = { Amount = 100, CurrencyCode = "EUR", CountryCode = "DK", LanguageCode = "da", PaymentProductId = 123}
			};

			var text = request.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);

			// Assert
			AssertResponseOk(responseText);
		}

		[Test, Ignore("")]
		public void SetPayment()
		{
			// Arrange
			var request = new SetPayment()
			{
				Meta = {MerchantId = 7454},
				Payment =
				{
					PaymentProductId = 123,
					OrderId = 1200003770
				}
			};

			var text = request.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);

			// Assert
			AssertResponseOk(responseText);
		}

		[Test, Ignore("")]
		public void GetOrder()
		{
			// Arrange
			var request = new GetOrder()
			{
				Meta = { MerchantId = 7454 },
				Order =
				{
					OrderId = 1200003770
				}
			};

			var text = request.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);

			// Assert
			AssertResponseOk(responseText);
		}

		[Test, Ignore("")]
		public void GetOrderStatus()
		{
			var request = new GetOrderStatus()
			{
				Meta = {MerchantId = 7454, Version = "2.0"},
				Order =
				{
					OrderId = 1200003770
				}
			};

			var text = request.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);

			// Assert
			AssertResponseOk(responseText);

			var response = new GetOrderStatus();
			response.FromModifiedXml(new ModifiedXmlDocument(responseText), string.Empty);

			Assert.NotNull(response.Response);
			Assert.AreEqual(1200003770, response.Response.Status.OrderId);
		}

		[Test, Ignore("")]
		public void RefundOrder()
		{
			var request = new DoRefund()
			{
				Meta = {MerchantId = 7454},
				Payment = 
				{
					OrderId = 1200003945,
					MerchantReference = "Reference111-4222",
					Amount = 1000
				}
			};

			var text = request.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);
		}

		[Test, Ignore("")]
		public void CancelOrder()
		{
			// Arrange
			var request = new CancelOrder()
			{
				Meta = { MerchantId = 7454 },
				Order =
				{
					OrderId = 1200003770,
					MerchantReference = "MyReference12345"
				}
			};

			var text = request.ToString();
			var caller = new ServiceApiCaller("HTTPS://ps.gcsip.nl/wdl/wdl");

			// Act
			var responseText = caller.Send(text);

			// Assert
			AssertResponseOk(responseText);
		}

		private void AssertResponseOk(string text)
		{
			Assert.AreNotEqual(string.Empty, text);

			var responseDocument = new ModifiedXmlDocument(text);
			var result = responseDocument.GetStringFromXml("XML/REQUEST/RESPONSE/RESULT");

			if (result != "OK")
			{
				var message = responseDocument.GetStringFromXml("XML/REQUEST/RESPONSE/ERROR/MESSAGE");
				Assert.Fail(message);
			}
		}
	}
}

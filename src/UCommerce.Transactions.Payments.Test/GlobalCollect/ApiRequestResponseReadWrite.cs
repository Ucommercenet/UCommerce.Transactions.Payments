using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using Ucommerce.Transactions.Payments.GlobalCollect;
using Ucommerce.Transactions.Payments.GlobalCollect.Api;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.Test.GlobalCollect
{
	[TestFixture]
	public class ApiRequestResponseReadWrite
	{
		[Test]
		public void Write_Meta()
		{
			var meta = new Meta()
			{
				MerchantId = 123,
				IpAddress = "123.123.123.123",
				Version = "2.0"
			};

			var sb = new StringBuilder();
			meta.AddToStringBuilder(sb);
			var metaAsString = sb.ToString();

			Assert.AreEqual("<META><MERCHANTID>123</MERCHANTID><IPADDRESS>123.123.123.123</IPADDRESS><VERSION>2.0</VERSION></META>", metaAsString);
		}

		[Test]
		public void Write_Partial_Meta()
		{
			var meta = new Meta()
			{
				MerchantId = 123
			};

			var sb = new StringBuilder();
			meta.AddToStringBuilder(sb);
			var metaAsString = sb.ToString();

			Assert.AreEqual("<META><MERCHANTID>123</MERCHANTID></META>", metaAsString);
		}

		[Test]
		public void Read_Meta()
		{
			var doc = new ModifiedXmlDocument("<META><MERCHANTID>123</MERCHANTID><IPADDRESS>123.123.123.123</IPADDRESS><VERSION>2.0</VERSION></META>");
			var meta = new Meta(doc, string.Empty);

			Assert.AreEqual(123, meta.MerchantId);
			Assert.AreEqual("123.123.123.123", meta.IpAddress);
			Assert.AreEqual("2.0", meta.Version);
		}

		[Test]
		public void Read_Partial_Meta()
		{
			var doc = new ModifiedXmlDocument("<META><MERCHANTID>123</MERCHANTID></META>");
			var meta = new Meta(doc, string.Empty);

			Assert.AreEqual(123, meta.MerchantId);
			Assert.IsNull(meta.IpAddress);
			Assert.IsNull(meta.Version);
		}

		[Test]
		public void Read_Order()
		{
			var doc = new ModifiedXmlDocument(OrderText);
			var order = new ApiOrder(doc, string.Empty);

			Assert.AreEqual(9998990005, order.OrderId);
			Assert.AreEqual(29990, order.Amount);
			Assert.AreEqual("EUR", order.CurrencyCode);
			Assert.AreEqual("NL", order.CountryCode);
			Assert.AreEqual("nl", order.LanguageCode);
			Assert.IsFalse(order.MerchantOrderId.HasValue);
		}

		[Test]
		public void Write_Then_Read_Order()
		{
			var order = new ApiOrder()
			{
				OrderId = 9998990005,
				MerchantOrderId = 12,
				Amount = 29990,
				CurrencyCode = "EUR",
				CountryCode = "DK",
				LanguageCode = "dn"
			};

			var sb = new StringBuilder();
			order.AddToStringBuilder(sb);
			var orderAsString = sb.ToString();

			var order2 = new ApiOrder(new ModifiedXmlDocument(orderAsString), string.Empty);

			Assert.AreEqual(order.OrderId, order2.OrderId);
			Assert.AreEqual(order.MerchantOrderId, order2.MerchantOrderId);
			Assert.AreEqual(order.Amount, order2.Amount);
			Assert.AreEqual(order.CurrencyCode, order2.CurrencyCode);
			Assert.AreEqual(order.CountryCode, order2.CountryCode);
			Assert.AreEqual(order.LanguageCode, order2.LanguageCode);
		}

		[Test]
		public void Write_Then_Read_ApiAction()
		{
			var action = new ApiAction("GET_DIRECTORY");

			var actionAsString = ConvertApiDataPartToString(action);
			var action2 = new ApiAction(new ModifiedXmlDocument(actionAsString), string.Empty);

			Assert.AreEqual(action.Name, action2.Name);
			Assert.AreEqual("GET_DIRECTORY", action2.Name);
		}

		[Test]
		public void Write_Then_Read_General()
		{
			var general = new General
			{
				PaymentProductId = 1,
				CountryCode = "NL",
				CurrencyCode = "EUR"
			};

			var text = ConvertApiDataPartToString(general);
			var general2 = new General(new ModifiedXmlDocument(text), string.Empty);

			Assert.AreEqual(general.PaymentProductId, general2.PaymentProductId);
			Assert.AreEqual(general.CountryCode, general2.CountryCode);
			Assert.AreEqual(general.CurrencyCode, general2.CurrencyCode);

			Assert.AreEqual(1, general2.PaymentProductId);
			Assert.AreEqual("NL", general2.CountryCode);
			Assert.AreEqual("EUR", general2.CurrencyCode);
		}

		[Test]
		public void Write_Then_Read_GetDirectory()
		{
			// Arrange
			var getDirectory = new GetDirectory
			{
				Meta = {MerchantId = 1, IpAddress = "123.123.123.123", Version = "1.0"},
				General = {PaymentProductId = 1, CountryCode = "NL", CurrencyCode = "EUR"}
			};

			// Act
			var text = ConvertApiDataPartToString(getDirectory);

			var getDirectory2 = new GetDirectory();
			getDirectory2.FromModifiedXml(new ModifiedXmlDocument(text), string.Empty);

			// Assert
			Assert.AreEqual(1, getDirectory2.Meta.MerchantId);
			Assert.AreEqual("123.123.123.123", getDirectory2.Meta.IpAddress);
			Assert.AreEqual("1.0", getDirectory2.Meta.Version);

			Assert.AreEqual(1, getDirectory2.General.PaymentProductId);
			Assert.AreEqual("NL", getDirectory2.General.CountryCode);
			Assert.AreEqual("EUR", getDirectory2.General.CurrencyCode);
		}

		[Test]
		public void Write_Then_Read_GetPaymentProducts()
		{
			// Arrange
			var request = new GetPaymentProducts
			{
				Meta = {MerchantId = 1, IpAddress = "123.123.123.123", Version = "1.0"},
				General = {CountryCode = "NL", LanguageCode = "en"}
			};

			// Act
			var text = ConvertApiDataPartToString(request);

			var request2 = new GetPaymentProducts();
			request2.FromModifiedXml(new ModifiedXmlDocument(text), string.Empty);

			// Assert
			Assert.AreEqual(1, request2.Meta.MerchantId);
			Assert.AreEqual("123.123.123.123", request2.Meta.IpAddress);
			Assert.AreEqual("1.0", request2.Meta.Version);

			Assert.AreEqual("NL", request2.General.CountryCode);
			Assert.AreEqual("en", request2.General.LanguageCode);
		}

		[Test]
		public void Write_Then_Read_InsertOrderWithPayment()
		{
			// Arrange
			var request = new InsertOrderWithPayment
			{
				Meta = {MerchantId = 1},
				Order = {Amount = 100, CurrencyCode = "EUR", CountryCode = "DK", LanguageCode = "dn"},
				Payment = {PaymentProductId = 3, Amount = 100, CurrencyCode = "EUR", CountryCode = "DK", LanguageCode = "dn"}
			};

			// Act
			var text = ConvertApiDataPartToString(request);

			var request2 = new InsertOrderWithPayment();
			request2.FromModifiedXml(new ModifiedXmlDocument(text), string.Empty);

			// Assert
			Assert.AreEqual(1, request2.Meta.MerchantId);
		
			Assert.AreEqual(100, request2.Order.Amount);
			Assert.AreEqual("EUR", request2.Order.CurrencyCode);
			Assert.AreEqual("DK", request2.Order.CountryCode);
			Assert.AreEqual("dn", request2.Order.LanguageCode);

			Assert.AreEqual(3, request2.Payment.PaymentProductId);
			Assert.AreEqual(100, request2.Payment.Amount);
			Assert.AreEqual("EUR", request2.Payment.CurrencyCode);
			Assert.AreEqual("DK", request2.Payment.CountryCode);
			Assert.AreEqual("dn", request2.Payment.LanguageCode);
		}

		[Test]
		public void Write_Then_Read_SetPayment()
		{
			// Arrange
			var request = new SetPayment
			{
				Meta = { MerchantId = 7454 },
				Payment = { Amount = 12345, CountryCode = "DK", OrderId = 1234, MerchantReference = "MyPaymentReference", EffortId = 1 }
			};

			// Act
			var text = ConvertApiDataPartToString(request);
			var request2 = new SetPayment();
			request2.FromModifiedXml(new ModifiedXmlDocument(text), string.Empty);

			// Assert
			Assert.AreEqual(7454, request2.Meta.MerchantId);
			Assert.AreEqual(12345, request2.Payment.Amount);
			Assert.AreEqual("DK", request2.Payment.CountryCode);
			Assert.AreEqual(1234, request2.Payment.OrderId);
			Assert.AreEqual("MyPaymentReference", request2.Payment.MerchantReference);
			Assert.AreEqual(1, request2.Payment.EffortId);
		}

		[Test]
		public void Write_SetPayment_Amount_Not_Present()
		{
			// Arrange
			var request = new SetPayment()
			{
				Meta = { MerchantId = 7454 },
				Payment =
				{
					PaymentProductId = 123,
					OrderId = 1200003770
				}
			};

			// Act
			var text = ConvertApiDataPartToString(request);
			var request2 = new SetPayment();
			request2.FromModifiedXml(new ModifiedXmlDocument(text), string.Empty);

			// Assert
			Assert.AreEqual(7454, request2.Meta.MerchantId);
			Assert.IsFalse(request2.Payment.Amount.HasValue);
			Assert.AreEqual(123, request2.Payment.PaymentProductId);
			Assert.AreEqual(1200003770, request2.Payment.OrderId);
		}

		[Test]
		public void Read_GetPaymentProductsResponse()
		{
			// Arrange
			var response = new GetPaymentProducts();
			
			//Act
			response.FromModifiedXml(new ModifiedXmlDocument(GetPaymentMethodsResponseText), string.Empty );

			// Assert
			Assert.IsNotNull(response.Response);
			Assert.AreEqual("OK", response.Response.Result);
			Assert.AreEqual(841604, response.Response.Meta.RequestId);
			Assert.IsNotNull(response.Response.PaymentProducts);
			Assert.AreEqual(7, response.Response.PaymentProducts.Count);
			Assert.Contains("Credit Card / Debit Card", response.Response.PaymentProducts.Select(x => x.PaymentMethodName).ToList());
			Assert.Contains("American Express", response.Response.PaymentProducts.Select(x => x.PaymentProductName).ToList());
		}

		[Test]
		public void Read_InsertOrderWithPaymentReponse()
		{
			// Arrange
			var response = new InsertOrderWithPayment();

			// Act
			response.FromModifiedXml(new ModifiedXmlDocument(InsertOrderWithPaymentSuccessReplyText), string.Empty);

			// Assert.
			Assert.IsNotNull(response.Response);
			Assert.AreEqual("OK", response.Response.Result);
			Assert.AreEqual(1199821, response.Response.Meta.RequestId);
			Assert.IsNotNull(response.Response.PaymentRows);
			Assert.AreEqual(1, response.Response.PaymentRows.Count);
		}

		[Test]
		public void Read_ReplyWithErrors()
		{
			// Arrange
			// Act
			var errorChecker = new ErrorChecker(ReplyWithErrorsText);

			// Assert.
			Assert.AreEqual("NOK", errorChecker.Result);
			Assert.IsNotNull(errorChecker.Errors);
			Assert.AreEqual(2, errorChecker.Errors.Count());
			Assert.Contains(21000100, errorChecker.Errors.Select(x => x.Code).ToList());
			Assert.Contains("REQUEST 1189457 VALUE dn OF FIELD ORDER.LANGUAGECODE IS NOT A VALID LANGUAGECODE", errorChecker.Errors.Select(x => x.Message).ToList());
			Assert.Contains("REQUEST 1189457 VALUE dn OF FIELD PAYMENT.LANGUAGECODE IS NOT A VALID LANGUAGECODE", errorChecker.Errors.Select(x => x.Message).ToList());
		}

		[Test]
		public void Read_ReplyWithErrors2()
		{
			// Arrange
			// Act
			var errorChecker = new ErrorChecker(InsertOrderFailedMessage);

			// Assert.
			Assert.AreEqual("NOK", errorChecker.Result);
			Assert.IsNotNull(errorChecker.Errors);
			Assert.AreEqual(1, errorChecker.Errors.Count());
		}
		[Test]
		public void Read_ReplyWithNoErrors()
		{
			// Arrange
			// Act
			var errorChecker = new ErrorChecker(GetPaymentMethodsResponseText);

			// Assert.
			Assert.AreEqual("OK", errorChecker.Result);
			Assert.IsNotNull(errorChecker.Errors);
			Assert.AreEqual(0, errorChecker.Errors.Count());
		}

		[Test]
		public void ReadGetOrderStatusResponse_With_Errors()
		{
			// Arrange
			// Act
			var response = new GetOrderStatus();
			response.FromModifiedXml(new ModifiedXmlDocument(OrderStatusTextWithErrors), string.Empty);

			// Assert
			Assert.AreEqual("OK", response.Response.Result);
			Assert.AreEqual(100, response.Response.Status.StatusId);
			Assert.AreEqual(1, response.Response.Status.Errors.Count);
			Assert.AreEqual("B", response.Response.Status.Errors.First().Type);
			Assert.AreEqual(430396, response.Response.Status.Errors.First().Code);
			Assert.AreEqual("430396 Not authorised", response.Response.Status.Errors.First().Message);
		}

		/// <summary>
		/// SCENARIO: Global Collect only accepts values with certain lengths. When creating the request
		/// for Global Collect we encode illegal XML values like &, <, >, and ", ' and this potentially
		/// leads to a situation where the encoded values is truncated thus rendering the XML invalid,
		/// e.g. &amp; cut off by one char &amp will make the XML invalid because the & is enterpreted
		/// as a "normal" unencoded char.
		/// </summary>
		[Test]
		public void ErrorReaderIsRobust()
		{
			var sb = new StringBuilder();
			// Need this to encode chars and cut off at the end to
			// create an incomplete HTML encoded value, e.g. &amp; => &amp
			PartsHelper.AddString(sb, "legalValue&", "someTag", "legalValue".Length + "&amp".Length);

			Assert.That(sb.ToString(), Is.EqualTo("<someTag>legalValue</someTag>"));
		}

		private static string ConvertApiDataPartToString(IApiDataPart part)
		{
			var sb = new StringBuilder();
			part.AddToStringBuilder(sb);
			return sb.ToString();
		}

		#region Huuuuuuuuuuuge constant strings
		private const string OrderText = @"<ORDER>
<ORDERID>9998990005</ORDERID>
<ORDERTYPE>1</ORDERTYPE>
<AMOUNT>29990</AMOUNT>
<CURRENCYCODE>EUR</CURRENCYCODE>
<CUSTOMERID>14</CUSTOMERID>
<IPADDRESSCUSTOMER>192.168.203.1</IPADDRESSCUSTOMER>
<FIRSTNAME>Johan</FIRSTNAME>
<SURNAME>Cruijff</SURNAME>
<STREET>Camp Nou</STREET>
<HOUSENUMBER>14</HOUSENUMBER>
<CITY>Barcelona</CITY>
<ZIP>1000 AA</ZIP>
<STATE>Catalunie</STATE>
<EMAIL>aconsumer@company.com</EMAIL>
<EMAILTYPEINDICATOR>1</EMAILTYPEINDICATOR>
<COMPANYNAME>Cruijff Sports</COMPANYNAME>
<VATNUMBER>VAT 14</VATNUMBER>
<INVOICEDATE>20030301000000</INVOICEDATE>
<INVOICENUMBER>20030222000000000001</INVOICENUMBER>
<ORDERDATE>20030222160000</ORDERDATE>
<COUNTRYCODE>NL</COUNTRYCODE>
<LANGUAGECODE>nl</LANGUAGECODE>
<RESELLERID>1</RESELLERID>
</ORDER>";

		private const string GetPaymentMethodsResponseText = @"<XML>
  <REQUEST>
    <ACTION>GET_PAYMENTPRODUCTS</ACTION>
    <META>
      <MERCHANTID>7454</MERCHANTID>
      <REQUESTIPADDRESS>46.16.250.68</REQUESTIPADDRESS>
    </META>
    <PARAMS>
      <GENERAL>
        <COUNTRYCODE>GB</COUNTRYCODE>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <LANGUAGECODE>en</LANGUAGECODE>
      </GENERAL>
    </PARAMS>
    <RESPONSE>
      <RESULT>OK</RESULT>
      <META>
        <REQUESTID>841604</REQUESTID>
        <RESPONSEDATETIME>20140523154516</RESPONSEDATETIME>
      </META>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>2</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>American Express</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_amex.png</PAYMENTPRODUCTLOGO>
      </ROW>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>123</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>Dankort - Visa branded</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_dankort.png</PAYMENTPRODUCTLOGO>
      </ROW>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>125</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>JCB</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_jcb.png</PAYMENTPRODUCTLOGO>
      </ROW>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>122</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>Visa Electron</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_electron.png</PAYMENTPRODUCTLOGO>
      </ROW>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>3</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>MasterCard</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_mc.png</PAYMENTPRODUCTLOGO>
      </ROW>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>1</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>Visa</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_visa.png</PAYMENTPRODUCTLOGO>
      </ROW>
      <ROW>
        <PAYMENTMETHODNAME>Credit Card / Debit Card</PAYMENTMETHODNAME>
        <PAYMENTPRODUCTID>130</PAYMENTPRODUCTID>
        <PAYMENTPRODUCTNAME>Carte Bancaire</PAYMENTPRODUCTNAME>
        <ORDERTYPEINDICATOR>7</ORDERTYPEINDICATOR>
        <MINAMOUNT></MINAMOUNT>
        <MAXAMOUNT>1000000</MAXAMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <PAYMENTPRODUCTLOGO>https://eu.gcsip.nl/hpp/externals/pl/gc_cb.png</PAYMENTPRODUCTLOGO>
      </ROW>
    </RESPONSE>
  </REQUEST>
</XML>";

		private const string ReplyWithErrorsText = @"<XML>
  <REQUEST>
    <ACTION>INSERT_ORDERWITHPAYMENT</ACTION>
    <META>
      <MERCHANTID>7454</MERCHANTID>
      <REQUESTIPADDRESS>46.16.250.69</REQUESTIPADDRESS>
    </META>
    <PARAMS>
      <ORDER>
        <AMOUNT>100</AMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <LANGUAGECODE>dn</LANGUAGECODE>
        <COUNTRYCODE>DK</COUNTRYCODE>
      </ORDER>
      <PAYMENT>
        <PAYMENTPRODUCTID>3</PAYMENTPRODUCTID>
        <AMOUNT>100</AMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <LANGUAGECODE>dn</LANGUAGECODE>
        <COUNTRYCODE>DK</COUNTRYCODE>
      </PAYMENT>
    </PARAMS>
    <RESPONSE>
      <RESULT>NOK</RESULT>
      <META>
        <REQUESTID>1189457</REQUESTID>
        <RESPONSEDATETIME>20140527094531</RESPONSEDATETIME>
      </META>
      <ERROR>
        <CODE>21000100</CODE>
        <MESSAGE>REQUEST 1189457 VALUE dn OF FIELD ORDER.LANGUAGECODE IS NOT A VALID LANGUAGECODE</MESSAGE>
      </ERROR>
      <ERROR>
        <CODE>21000100</CODE>
        <MESSAGE>REQUEST 1189457 VALUE dn OF FIELD PAYMENT.LANGUAGECODE IS NOT A VALID LANGUAGECODE</MESSAGE>
      </ERROR>
    </RESPONSE>
  </REQUEST>
</XML>";

		private const string InsertOrderWithPaymentSuccessReplyText = @"<XML>
  <REQUEST>
    <ACTION>INSERT_ORDERWITHPAYMENT</ACTION>
    <META>
      <MERCHANTID>7454</MERCHANTID>
      <REQUESTIPADDRESS>46.16.250.69</REQUESTIPADDRESS>
    </META>
    <PARAMS>
      <ORDER>
        <AMOUNT>100</AMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <LANGUAGECODE>da</LANGUAGECODE>
        <COUNTRYCODE>DK</COUNTRYCODE>
        <MERCHANTREFERENCE>MyReference12345</MERCHANTREFERENCE>
      </ORDER>
      <PAYMENT>
        <PAYMENTPRODUCTID>123</PAYMENTPRODUCTID>
        <AMOUNT>100</AMOUNT>
        <CURRENCYCODE>EUR</CURRENCYCODE>
        <LANGUAGECODE>da</LANGUAGECODE>
        <COUNTRYCODE>DK</COUNTRYCODE>
      </PAYMENT>
    </PARAMS>
    <RESPONSE>
      <RESULT>OK</RESULT>
      <META>
        <REQUESTID>1199821</REQUESTID>
        <RESPONSEDATETIME>20140527104322</RESPONSEDATETIME>
      </META>
      <ROW>
        <STATUSDATE>20140527104322</STATUSDATE>
        <PAYMENTREFERENCE>0</PAYMENTREFERENCE>
        <ADDITIONALREFERENCE>MyReference12345</ADDITIONALREFERENCE>
        <ORDERID>1200003770</ORDERID>
        <EXTERNALREFERENCE>MyReference12345</EXTERNALREFERENCE>
        <EFFORTID>1</EFFORTID>
        <REF>000000745412000037700000100001</REF>
        <FORMACTION>https://eu.gcsip.nl/orb/orb?ACTION=DO_START&amp;REF=000000745412000037700000100001&amp;MAC=UkKnl%2FohzlNn5kym5rtkjBt3yDsmOkHsERsInpOCo7U%3D</FORMACTION>
        <FORMMETHOD>GET</FORMMETHOD>
        <ATTEMPTID>1</ATTEMPTID>
        <MERCHANTID>7454</MERCHANTID>
        <STATUSID>20</STATUSID>
        <RETURNMAC>LOWCCu2CtjGjOvwBSyrNU2y5lX5oB2TKGAxNIN/Gix4=</RETURNMAC>
        <MAC>UkKnl/ohzlNn5kym5rtkjBt3yDsmOkHsERsInpOCo7U=</MAC>
      </ROW>
    </RESPONSE>
  </REQUEST>
</XML>";

		private const string InsertOrderFailedMessage = @"<XML><REQUEST><ACTION>INSERT_ORDERWITHPAYMENT</ACTION><META><MERCHANTID>7454</MERCHANTID><REQUESTIPADDRESS>46.16.250.68</REQUESTIPADDRESS></META><PARAMS><ORDER><AMOUNT>19182</AMOUNT><CURRENCYCODE>EUR</CURRENCYCODE><LANGUAGECODE>en</LANGUAGECODE><COUNTRYCODE>DK</COUNTRYCODE><MERCHANTREFERENCE>1026</MERCHANTREFERENCE></ORDER><PAYMENT><PAYMENTPRODUCTID>123</PAYMENTPRODUCTID><AMOUNT>19182</AMOUNT><CURRENCYCODE>EUR</CURRENCYCODE><LANGUAGECODE>en</LANGUAGECODE><COUNTRYCODE>DK</COUNTRYCODE><RETURNURL>http://sctest:80/9/1026/PaymentProcessor.axd&amp;ADDITIONALREFERENCE=1026</RETURNURL></PAYMENT></PARAMS><RESPONSE><RESULT>NOK</RESULT><META><REQUESTID>15930</REQUESTID><RESPONSEDATETIME>20140606142914</RESPONSEDATETIME></META><ERROR><CODE>300620</CODE><MESSAGE>REQUEST 15930: MERCHANTREFERENCE 1026 ALREADY EXISTS</MESSAGE></ERROR></RESPONSE></REQUEST></XML>";

		private const string OrderStatusTextWithErrors = @"<XML><REQUEST><ACTION>GET_ORDERSTATUS</ACTION><META><MERCHANTID>7454</MERCHANTID><VERSION>2.0</VERSION><REQUESTIPADDRESS>46.16.250.69</REQUESTIPADDRESS></META><PARAMS><ORDER><ORDERID>1200004368</ORDERID></ORDER></PARAMS><RESPONSE><RESULT>OK</RESULT><META><REQUESTID>380571</REQUESTID><RESPONSEDATETIME>20140912111300</RESPONSEDATETIME></META><STATUS><STATUSDATE>20140912111300</STATUSDATE><PAYMENTMETHODID>1</PAYMENTMETHODID><MERCHANTREFERENCE>MyReference-25</MERCHANTREFERENCE><FRAUDRESULT>A</FRAUDRESULT><ATTEMPTID>1</ATTEMPTID><PAYMENTREFERENCE>0</PAYMENTREFERENCE><AMOUNT>34344</AMOUNT><EXPIRYDATE>0318</EXPIRYDATE><MERCHANTID>7454</MERCHANTID><ORDERID>1200004368</ORDERID><STATUSID>100</STATUSID><CREDITCARDNUMBER>************7107</CREDITCARDNUMBER><FRAUDCODE>0100</FRAUDCODE><EFFORTID>1</EFFORTID><CVVRESULT>0</CVVRESULT><CURRENCYCODE>EUR</CURRENCYCODE><PAYMENTPRODUCTID>123</PAYMENTPRODUCTID><ERRORS><ERROR><TYPE>B</TYPE><CODE>430396</CODE><MESSAGE>430396 Not authorised</MESSAGE></ERROR></ERRORS></STATUS></RESPONSE></REQUEST></XML>";

		private const string ResponseTheErrorCheckerHasProblemsWith =
			@"<XML><REQUEST><ACTION>INSERT_ORDERWITHPAYMENT</ACTION><META><MERCHANTID>7899</MERCHANTID><REQUESTIPADDRESS>46.16.252.133</REQUESTIPADDRESS></META><PARAMS><ORDER><AMOUNT>15150</AMOUNT><CURRENCYCODE>GBP</CURRENCYCODE><LANGUAGECODE>en</LANGUAGECODE><COUNTRYCODE>GB</COUNTRYCODE><MERCHANTREFERENCE>1010017125</MERCHANTREFERENCE><FIRSTNAME>Kathleen</FIRSTNAME><SURNAME>Horton</SURNAME><STREET>10 Mandley Close Little Lever</STREET><CITY>Bolton</CITY><IPADDRESSCUSTOMER>78.149.209.206</IPADDRESSCUSTOMER><EMAIL>monarchblinds@tiscali.co.uk</EMAIL><ZIP>BL3 1PZ</ZIP><SHIPPINGFIRSTNAME>Kathleen</SHIPPINGFIRSTNAME><SHIPPINGSURNAME>Horton</SHIPPINGSURNAME><SHIPPINGSTREET>2a Higher Market Street Farnworth</SHIPPINGSTREET><SHIPPINGCITY>Bolton</SHIPPINGCITY><SHIPPINGCOUNTRYCODE>GB</SHIPPINGCOUNTRYCODE><SHIPPINGZIP>BL4 9AJ</SHIPPINGZIP><SHIPPINGCOMPANYNAME>Monarch Curtains & Blinds Ltd</SHIPPINGCOMPANYNAME></ORDER><PAYMENT><PAYMENTPRODUCTID>3</PAYMENTPRODUCTID><AMOUNT>15150</AMOUNT><CURRENCYCODE>GBP</CURRENCYCODE><LANGUAGECODE>en</LANGUAGECODE><COUNTRYCODE>GB</COUNTRYCODE><RETURNURL>https://www.georgjensen.com/10/4412/PaymentProcessor.axd</RETURNURL></PAYMENT></PARAMS><RESPONSE><RESULT>OK</RESULT><META><REQUESTID>15048</REQUESTID><RESPONSEDATETIME>20141015120336</RESPONSEDATETIME></META><ROW><STATUSDATE>20141015120336</STATUSDATE><PAYMENTREFERENCE>0</PAYMENTREFERENCE><ADDITIONALREFERENCE>1010017125</ADDITIONALREFERENCE><ORDERID>4000000091</ORDERID><EXTERNALREFERENCE>1010017125</EXTERNALREFERENCE><EFFORTID>1</EFFORTID><REF>000000789940000000910000100001</REF><FORMACTION>https://na.gcsip.com/orb/orb?ACTION=DO_START&amp;REF=000000789940000000910000100001&amp;MAC=RC4Cjl15XRiiPcsW%2FBDzT9CQLToZBCczFyANiwZpUyg%3D</FORMACTION><FORMMETHOD>GET</FORMMETHOD><ATTEMPTID>1</ATTEMPTID><MERCHANTID>7899</MERCHANTID><STATUSID>20</STATUSID><RETURNMAC>CVpk1QfFqCFioDgwXtzWxV0q+ARGOUYuJuW4anzpiGw=</RETURNMAC><MAC>RC4Cjl15XRiiPcsW/BDzT9CQLToZBCczFyANiwZpUyg=</MAC></ROW></RESPONSE></REQUEST></XML>";

		private const string ResponseWithIllegalChars =
			@"<XML><REQUEST><ACTION>INSERT_ORDERWITHPAYMENT</ACTION><META><MERCHANTID>7454</MERCHANTID><REQUESTIPADDRESS>46.16.250.69</REQUESTIPADDRESS></META><PARAMS><ORDER><AMOUNT>15525</AMOUNT><CURRENCYCODE>EUR</CURRENCYCODE><LANGUAGECODE>en</LANGUAGECODE><COUNTRYCODE>DK</COUNTRYCODE><MERCHANTREFERENCE>Reference-21102014-16</MERCHANTREFERENCE><FIRSTNAME>Søren! &lt;-&lt</FIRSTNAME><SURNAME>Spelling Lund! &lt;-&lt;&gt; #% &am</SURNAME><PHONENUMBER>4540385659! &lt;-&lt</PHONENUMBER><STREET>Studsgade 29! &lt;-&lt;&gt; #% &amp;</STREET><ADDITIONALADDRESSINFO>! &lt;-&lt;&gt; #% &amp;</ADDITIONALADDRESSINFO><CITY>Aarhus C! &lt;-&lt;&gt; #% &amp;</CITY><IPADDRESSCUSTOMER>127.0.0.1</IPADDRESSCUSTOMER><EMAIL>soren.spelling.lund@Ucommerce.dk</EMAIL><ZIP>8000! &lt</ZIP><COMPANYNAME>Ucommerce ApS! &lt;-&lt;&gt; #% &amp;</COMPANYNAME><SHIPPINGFIRSTNAME>Søren! &lt;-&lt</SHIPPINGFIRSTNAME><SHIPPINGSURNAME>Spelling Lund! &lt;-&lt;&gt; #% &am</SHIPPINGSURNAME><SHIPPINGSTREET>Studsgade 29! &lt;-&lt;&gt; #% &amp;</SHIPPINGSTREET><SHIPPINGADDITIONALADDRESSINFO>! &lt;-&lt;&gt; #% &amp;</SHIPPINGADDITIONALADDRESSINFO><SHIPPINGCITY>Aarhus C! &lt;-&lt;&gt; #% &amp;</SHIPPINGCITY><SHIPPINGCOUNTRYCODE>DK</SHIPPINGCOUNTRYCODE><SHIPPINGZIP>8000! &lt;</SHIPPINGZIP><SHIPPINGCOMPANYNAME>Ucommerce ApS! &lt;-&lt;&gt; #% &amp;</SHIPPINGCOMPANYNAME></ORDER><PAYMENT><PAYMENTPRODUCTID>123</PAYMENTPRODUCTID><AMOUNT>15525</AMOUNT><CURRENCYCODE>EUR</CURRENCYCODE><LANGUAGECODE>en</LANGUAGECODE><COUNTRYCODE>DK</COUNTRYCODE><RETURNURL>http://u7dev:80/8/1021/PaymentProcessor.axd</RETURNURL></PAYMENT></PARAMS><RESPONSE><RESULT>OK</RESULT><META><REQUESTID>528549</REQUESTID><RESPONSEDATETIME>20141023084712</RESPONSEDATETIME></META><ROW><STATUSDATE>20141023084713</STATUSDATE><PAYMENTREFERENCE>0</PAYMENTREFERENCE><ADDITIONALREFERENCE>Reference-21102014-1</ADDITIONALREFERENCE><ORDERID>1200004704</ORDERID><EXTERNALREFERENCE>Reference-21102014-16</EXTERNALREFERENCE><EFFORTID>1</EFFORTID><REF>000000745412000047040000100001</REF><FORMACTION>https://eu.gcsip.nl/orb/orb?ACTION=DO_START&amp;REF=000000745412000047040000100001&amp;MAC=%2BtHTw6x3wfS578L%2FFM0a%2BatJLhSr0e1wAYeajplryt4%3D</FORMACTION><FORMMETHOD>GET</FORMMETHOD><ATTEMPTID>1</ATTEMPTID><MERCHANTID>7454</MERCHANTID><STATUSID>20</STATUSID><RETURNMAC>2S2aHRhN6av6SEzduuJ2lD59L1977ZW2KjdMvSYFZJk=</RETURNMAC><MAC>+tHTw6x3wfS578L/FM0a+atJLhSr0e1wAYeajplryt4=</MAC></ROW></RESPONSE></REQUEST></XML>";

		#endregion Huuuuuuuuuuuge constant strings
	}
}

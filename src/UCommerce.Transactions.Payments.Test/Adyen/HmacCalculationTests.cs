using NUnit.Framework;
using UCommerce.Transactions.Payments.Adyen;

namespace UCommerce.Transactions.Payments.Test.Adyen
{
	[TestFixture]
	public class HmacCalculationTests
	{
		[Test]
		public void VerificationOfExampleValues()
		{
			const string signingString = "10000GBP2007-10-20Internet Order 123454aD37dJATestMerchant2007-10-11T11:00:00Z";
			const string sharedSecret = "Kah942*$7sdp0)";
			const string expectedHmac = "x58ZcRVL1H6y+XSeBGrySJ9ACVo=";

			var calculator = new HmacCalculator(sharedSecret);

			var mac = calculator.Execute(signingString);
			Assert.AreEqual(expectedHmac, mac);

			// Recalculate
			mac = calculator.Execute(signingString);
			Assert.AreEqual(expectedHmac, mac);

			// Recalculate with tampered data
			mac = calculator.Execute(signingString + "EVIL!");
			Assert.AreNotEqual(expectedHmac, mac);
		}

		[Test]
		public void VerificationOfExampleNumberTwo()
		{
			const string signingString = "AUTHORISED1211992213193029Internet Order 123454aD37dJA";
			const string sharedSecret = "Kah942*$7sdp0)";
			const string expectedHmac = "ytt3QxWoEhAskUzUne0P5VA9lPw=";

			var calculator = new HmacCalculator(sharedSecret);

			var mac = calculator.Execute(signingString);
			Assert.AreEqual(expectedHmac, mac);
		}
	}
}

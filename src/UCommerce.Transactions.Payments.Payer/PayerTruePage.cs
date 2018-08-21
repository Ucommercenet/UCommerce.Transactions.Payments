using System.Text;

namespace UCommerce.Transactions.Payments.Payer
{
	public class PayerTruePage : AbstractPageBuilder
	{
		/// <summary>
		/// Builds the head attributes.
		/// </summary>
		protected override void BuildHead(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("<title>Payer true page</title>");
		}

		/// <summary>
		/// Builds the body attributes.
		/// </summary>
		/// <param name="page">The page.</param>
		/// <param name="paymentRequest">The payment request.</param>
		protected override void BuildBody(StringBuilder page, PaymentRequest paymentRequest)
		{
			page.Append("TRUE");
		}
	}
}
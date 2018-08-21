using System.Collections.Generic;
using UCommerce.EntitiesV2;
using UCommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public interface IGlobalCollectService
	{
		IEnumerable<IPaymentData> CreatePayment(PaymentMethod paymentMethod, CreatePaymentRequestDto request);

		IEnumerable<IPaymentProduct> GetPaymentProducts(PaymentMethod paymentMethod, string languageCode, string countryCode, string currencyCode);

		void SettlePayment(PaymentMethod paymentMethod, int paymentProductId, long orderId);

		void CancelPayment(PaymentMethod paymentMethod, long orderId, string merchantReference);

		void RefundPayment(PaymentMethod paymentMethod, long orderId, string merchantReference, long amount);

		IOrderStatus GetOrderStatus(PaymentMethod paymentMethod, long orderId);
	}
}

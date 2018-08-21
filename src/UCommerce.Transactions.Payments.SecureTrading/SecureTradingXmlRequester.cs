using UCommerce.EntitiesV2;
using UCommerce.Extensions;

namespace UCommerce.Transactions.Payments.SecureTrading
{
	public class SecureTradingXmlRequester
	{
		public SecureTradingTransactionQueryXmlResponse TransactionQuery(string transactionId, PaymentMethod paymentMethod)
		{
			string webServiceAlias = paymentMethod.DynamicProperty<string>().WebServiceAlias;
			string sitereference = paymentMethod.DynamicProperty<string>().Sitereference;

			string xmlToSend = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
									<requestblock version=""3.67"">
										<alias>{0}</alias>
										<request type=""TRANSACTIONQUERY"">
											<filter>
												<sitereference>{1}</sitereference>
												<transactionreference>{2}</transactionreference>
											</filter>
										</request>
									</requestblock>",
							webServiceAlias,
							sitereference,
							transactionId);

			return new SecureTradingTransactionQueryXmlResponse(CreateRequest(xmlToSend, paymentMethod));
		}

		public SecureTradingRefundXmlResponse Refund(string transactionId, string orderReference, PaymentMethod paymentMethod)
		{
			string webServiceAlias = paymentMethod.DynamicProperty<string>().WebServiceAlias;
			string sitereference = paymentMethod.DynamicProperty<string>().Sitereference;

			string xmlToSend = string.Format(@"
									<requestblock version=""3.67""> 
										<alias>{0}</alias> 
										<request type=""REFUND""> 
											<merchant> 
												<orderreference>{1}</orderreference> 
											</merchant> 
											<operation> 
												<sitereference>{2}</sitereference> 
												<parenttransactionreference>{3}</parenttransactionreference> 
											</operation> 
										</request> 
									</requestblock>",
							webServiceAlias,
							orderReference,
							sitereference,
							transactionId);

			return new SecureTradingRefundXmlResponse(CreateRequest(xmlToSend, paymentMethod));
		}

		public SecureTradingTransactionUpdateXmlResponse UpdateSettleMentStatus(string transactionId, SecureTradingSettlementStatus settleStatus, PaymentMethod paymentMethod)
		{
			string webServiceAlias = paymentMethod.DynamicProperty<string>().WebServiceAlias;
			string sitereference = paymentMethod.DynamicProperty<string>().Sitereference;

			string xmlToSend = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?> 
									<requestblock version=""3.67""> 
										<alias>{0}</alias> 
										<request type=""TRANSACTIONUPDATE""> 
											<filter> 
												<sitereference>{1}</sitereference> 
												<transactionreference>{2}</transactionreference> 
											</filter> 
											<updates> 
												<settlement> 
													<settlestatus>{3}</settlestatus> 
												</settlement> 
											</updates> 
										</request> 
									</requestblock>",
								webServiceAlias,
								sitereference,
								transactionId,
								(int)settleStatus);
			
			return new SecureTradingTransactionUpdateXmlResponse(CreateRequest(xmlToSend, paymentMethod));
		}

		private string CreateRequest(string xmlToSend, PaymentMethod paymentMethod)
		{
			string webServiceAlias = paymentMethod.DynamicProperty<string>().WebServiceAlias;
			string webServicePassword = paymentMethod.DynamicProperty<string>().WebServicePassword;

			string serviceUrl = "https://webservices.securetrading.net:443/xml/";
			
			return new XmlHttpPost(
					serviceUrl, 
					xmlToSend, 
					webServiceAlias,
					webServicePassword)
				.Request();
		}
	}
}

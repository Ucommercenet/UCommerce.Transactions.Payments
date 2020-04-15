using System.Linq;
using System.Security;
using System.Web;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.GlobalCollect
{
	public class GlobalCollectHttpPaymentExtractor : UrlPaymentExtractor
	{
		public override Payment Extract(HttpRequest httpRequest)
		{
			Payment payment = base.Extract(httpRequest);

			// Check Ref and ReturnMac, if present.
			var Ref = httpRequest["REF"];
			var returnMac = httpRequest["RETURNMAC"];

			if (string.IsNullOrEmpty(Ref) || Ref != payment["Ref"])
			{
				throw new SecurityException(string.Format("The REF parameter of the request does not match the expected value. Was: {0}. Expected {1}.", Ref, payment["Ref"]));
			}

			if (string.IsNullOrEmpty(returnMac) || returnMac != payment["ReturnMac"])
			{
				throw new SecurityException(string.Format("The RETURNMAC parameter of the request does not match the expected value. Was: {0}. Expected {1}.", returnMac, payment["ReturnMac"]));
			}

			return payment;
		}
	}
}

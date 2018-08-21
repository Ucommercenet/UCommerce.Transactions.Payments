namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public class GlobalCollectConstants
	{
		public static string TestApiServiceUsingIpRestriction {
			get { return "HTTPS://ps.gcsip.nl/wdl/wdl"; }
		}

		public static string TestApiServiceUsingClientAuthentication
		{
			get { return "HTTPS://ca.gcsip.nl/wdl/wdl"; }
		}

		public static string LiveApiServiceUsingIpRestriction
		{
			get { return "HTTPS://ps.gcsip.com/wdl/wdl"; }
		}

		public static string LiveApiServiceUsingClientAuthentication
		{
			get { return "HTTPS://ca.gcsip.com/wdl/wdl"; }
		}
	}
}

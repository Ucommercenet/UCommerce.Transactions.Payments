using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class Address
	{
		public string FirstName { get; set; }

		public string LastName { get; set; }

		public string IpAddress { get; set; }

		public string PhoneNumber { get; set; }

		public string StreetLine1 { get; set; }

		public string StreetLine2 { get; set; }

		public string City { get; set; }

		public string Zip { get; set; }

		public string Email { get; set; }

		public string State { get; set; }

		public string CountryCode { get; set; }

		public string CompanyName { get; set; }
	}
}

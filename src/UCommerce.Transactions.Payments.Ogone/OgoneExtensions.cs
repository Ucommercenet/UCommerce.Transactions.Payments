using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.Ogone
{
	/// <summary>
	/// Extensions for working with Ogone
	/// </summary>
	public static class OgoneExtensions
	{
		public static string OgoneCultureCode(this PurchaseOrder target)
		{
			return target.CultureCode.Replace("-", "_");
		}
	}
}

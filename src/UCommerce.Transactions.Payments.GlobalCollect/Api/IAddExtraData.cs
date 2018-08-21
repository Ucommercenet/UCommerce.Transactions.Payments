using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api
{
	public interface IAddExtraData
	{
		/// <summary>
		/// Add an additional set of data to be included in the resulting MODIFIED XML
		/// </summary>
		/// <param name="s">The value to be added.</param>
		/// <param name="tag">The tag to include the value inside.</param>
		void AddExtraData(string s, string tag);
	}
}

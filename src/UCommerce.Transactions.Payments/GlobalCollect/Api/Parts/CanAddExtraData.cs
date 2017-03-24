using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public class ExtraData : IAddExtraData, IAddToStringBuilder
	{
		private readonly List<KeyValuePair<string, string>> _extraData = new List<KeyValuePair<string, string>>();

		public void AddExtraData(string s, string tag)
		{
			_extraData.Add(new KeyValuePair<string, string>(s, tag));
		}

		public virtual void AddToStringBuilder(StringBuilder sb)
		{
			foreach (var pair in _extraData)
			{
				PartsHelper.AddString(sb, pair.Key, pair.Value);
			}
		}
	}
}

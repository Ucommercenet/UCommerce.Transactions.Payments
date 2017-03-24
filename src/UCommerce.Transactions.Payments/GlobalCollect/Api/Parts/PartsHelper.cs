using System.Globalization;
using System.Text;
using UCommerce.Transactions.Payments.Common;

namespace UCommerce.Transactions.Payments.GlobalCollect.Api.Parts
{
	public static class PartsHelper
	{
		public static void AddString(StringBuilder sb, string text, string tag)
		{
			AddString(sb, text, tag, int.MaxValue);
		}

		public static void AddString(StringBuilder sb, string text, string tag, int maxsNumberOfCharacters)
		{
			if (!string.IsNullOrEmpty(text))
			{
				text = XmlHelper.ConvertToValidXmlText(text);
				if (text.Length > maxsNumberOfCharacters)
				{
					text = text.Substring(0, maxsNumberOfCharacters);
					text = RemoveTruncatedHtmlEncodedChars(text);
				}

				sb.Append(string.Format("<{0}>{1}</{0}>", tag, text));
			}
		}

		private static string RemoveTruncatedHtmlEncodedChars(string text)
		{
			int lastAmp = text.LastIndexOf("&");
			int lastSemiColon = text.LastIndexOf(";");

			if (lastAmp > lastSemiColon)
				return text.Substring(0, lastAmp);

			return text;
		}

		public static void AddLong(StringBuilder sb, long l, string tag)
		{
			AddString(sb, l.ToString(CultureInfo.InvariantCulture), tag);
		}

		public static void AddNullableInt(StringBuilder sb, int? i, string tag)
		{
			if (i.HasValue)
			{
				AddLong(sb, i.Value, tag);
			}
		}

		public static void AddNullableLong(StringBuilder sb, long? l, string tag)
		{
			if (l.HasValue)
			{
				AddLong(sb, l.Value, tag);
			}
		}
	}
}

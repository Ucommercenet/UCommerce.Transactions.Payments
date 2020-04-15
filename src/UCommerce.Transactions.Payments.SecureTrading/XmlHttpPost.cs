using System;
using System.IO;
using System.Net;
using System.Text;

namespace Ucommerce.Transactions.Payments.SecureTrading
{
	/// <summary>
	/// Allows for creating a HttpPost with xml and basic 64bit encoded security.
	/// </summary>
	public class XmlHttpPost
	{
		private readonly string _uri;
		private readonly string _xml;
		private readonly string _alias;
		private readonly string _password;

		public XmlHttpPost(string uri, string xml, string alias, string password)
		{
			_uri = uri;
			_xml = xml;
			_alias = alias;
			_password = password;
		}

		public string Request()
		{
			byte[] bytes = Encoding.UTF8.GetBytes(_xml);
			string auth = string.Format("{0}:{1}", _alias, _password);

			var request = (HttpWebRequest)WebRequest.Create(_uri);
			// Set values for the request back
			request.Method = "POST";
			request.ContentType = "application/xml";
			request.ContentLength = bytes.Length;
			request.Headers.Add(HttpRequestHeader.Authorization, "Basic " + EncodeTo64Utf8(auth));

			using (Stream putStream = request.GetRequestStream())
			{
				putStream.Write(bytes, 0, bytes.Length);
			}

			//send the request, read the response
			var response = (HttpWebResponse)request.GetResponse();

			var responseStream = response.GetResponseStream();
			var encoding = Encoding.GetEncoding("utf-8");
			var reader = new StreamReader(responseStream, encoding);

			return reader.ReadToEnd();
		}

		/// <summary>
		/// Encodes a string to Base64
		/// </summary>
		/// <param name="input"></param>
		/// <returns>Base64 encoded string</returns>
		private string EncodeTo64Utf8(string input)
		{
			byte[] toEncodeAsBytes = Encoding.UTF8.GetBytes(input);
			string returnValue = Convert.ToBase64String(toEncodeAsBytes);

			return returnValue;
		}
	}
}

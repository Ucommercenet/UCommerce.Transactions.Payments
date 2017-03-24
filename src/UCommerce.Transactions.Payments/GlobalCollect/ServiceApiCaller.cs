using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace UCommerce.Transactions.Payments.GlobalCollect
{
	public class ServiceApiCaller
	{
		private readonly string _serviceUrl;

		public ServiceApiCaller(string serviceUrl)
		{
			_serviceUrl = serviceUrl;
		}

		public string Send(string message)
		{
			var webRequest = CreateWebRequest();
			AddMessageToRequest(webRequest, message);

			var response = webRequest.GetResponse();

			string responseMessage = ReadResponseMessage(response);

			return responseMessage;
		}

		private WebRequest CreateWebRequest()
		{
			var request = WebRequest.Create(_serviceUrl);
			request.Method = "POST";
			request.ContentType = "text/xml; charset=utf-8";

			return request;
		}

		private void AddMessageToRequest(WebRequest webRequest, string message)
		{
			var messageBytes = Encoding.UTF8.GetBytes(message);

			webRequest.ContentLength = messageBytes.Length;

			var stream = webRequest.GetRequestStream();
			stream.Write(messageBytes, 0, messageBytes.Length);
			stream.Close();
		}

		private string ReadResponseMessage(WebResponse response)
		{
			var responseStream = response.GetResponseStream();
			if (responseStream == null)
			{
				throw new Exception("The response did not contain any response stream.");
			}

			using (var streamReader = new StreamReader(responseStream))
			{
				return streamReader.ReadToEnd();
			}
		}
	}
}

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using UCommerce.EntitiesV2;
using UCommerce.Extensions;

namespace UCommerce.Transactions.Payments.MultiSafepay
{
    /// <summary>
    /// Sends an XML request to MultiSafepay and returns an XML-element containing the response.
    /// </summary>
    public class MultiSafepayHttpRequester
    {
        public XmlElement Request(string xmlRequestString, PaymentMethod paymentMethod)
        {
	        bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;

            string apiURl = testMode ? "https://testapi.multisafepay.com/ewx/" : "https://api.multisafepay.com/ewx/";

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(apiURl);
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = Encoding.UTF8.GetByteCount(xmlRequestString);
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";

            var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream());
            streamWriter.Write(xmlRequestString);
            streamWriter.Close();

            var httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            Stream responseStream = httpWebResponse.GetResponseStream();
            if (responseStream == null)
                throw new NullReferenceException("The HTTP response stream from MultiSafepay is empty.");

            var streamReader = new StreamReader(responseStream);

            string xmlstring = streamReader.ReadToEnd();
            
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xmlstring);

            XmlElement xmlElement = xmlDocument.DocumentElement;
            if (xmlElement == null)
                throw new NullReferenceException("XML response from MultiSafepay is empty, expected XML document.");

            if (xmlElement.Attributes == null)
                throw new NullReferenceException("XML response from MultiSafepay didn't contain any attributes, expected a 'result' attribute on the root element.");

            return xmlElement;
        }
    }
}

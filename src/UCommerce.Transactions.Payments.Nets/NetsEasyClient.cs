using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Ucommerce.Transactions.Payments.Nets.Api;

namespace Ucommerce.Transactions.Payments.Nets
{
    public class NetsEasyClient
    {
        private readonly NetsEasyClientConfig _config;

        public NetsEasyClient(NetsEasyClientConfig config)
        {
            _config = config;
        }

        public Task<NetsPaymentResult> CreatePaymentAsync(object data)
        {
            var request = CreateWebRequest(_config.Authorization, "/v1/payments/", "POST");
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();

                Task.FromResult(result);
            }
        }

        private HttpWebRequest CreateWebRequest(
            string auth,
            string resource,
            string method)
        {
            string requestUriString = _config.BaseUrl + resource;
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUriString);
            httpWebRequest.Headers["Authorization"] = auth;
            httpWebRequest.Method = method;
            return httpWebRequest;
        }

    }
}

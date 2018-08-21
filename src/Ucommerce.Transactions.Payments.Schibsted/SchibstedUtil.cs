using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Security;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using UCommerce.EntitiesV2;
using UCommerce.Transactions.Payments.Common;

namespace UCommerce.Transactions.Payments.Schibsted
{
    public class SchibstedUtil
    {
        private readonly string _schibstedOAuthBaseUrl;
        private readonly string _schibstedApiBaseUrl;

        public SchibstedUtil(bool test)
        {
            if (test)
            {
                _schibstedOAuthBaseUrl = "https://stage.payment.schibsted.no/oauth/";
                _schibstedApiBaseUrl = "https://stage.payment.schibsted.no/api/2";
            }
            else
            {
                _schibstedOAuthBaseUrl = "https://payment.schibsted.no/oauth/";
                _schibstedApiBaseUrl = "https://payment.schibsted.no/api/2";
            }
        }

        public SppContainer<T> SchibstedApiGet<T>(string endpoint, string oauthToken)
        {
            var apiCallUrl = _schibstedApiBaseUrl + endpoint + "?oauth_token=" + oauthToken;
            
            var responseStream = WebRequest.Create(apiCallUrl).GetResponse().GetResponseStream();
            var orderStatusJson = new StreamReader(responseStream).ReadToEnd();

            return JsonStringToSppContainer<T>(orderStatusJson);
        }

        public SppContainer<T> SchibstedApiPost<T>(string endpoint, IDictionary<string, string> postData)
        {
            var postResult = new HttpPost(_schibstedApiBaseUrl + endpoint, postData).GetString();
            return JsonStringToSppContainer<T>(postResult);
        }

        private SppContainer<T> JsonStringToSppContainer<T>(string jsonData)
        {
            var jsonSerializer = new DataContractJsonSerializer(typeof(SppContainer<T>));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

            return jsonSerializer.ReadObject(ms) as SppContainer<T>;
        }

        public SppCallbackContainer DecodeSignedRequest(string signedRequest, string signatureSecret)
        {
            var requestArr = signedRequest.Split('.');
            var encodedSig = requestArr[0];
            var payload = requestArr[1];

            var hashComputer = new SchibstedSha256Computer();
            var expectedSig = hashComputer.ComputeHash(payload, signatureSecret, true);

            var data = Base64UrlDecode(payload);

            if (expectedSig != encodedSig)
                throw new SecurityException("Checksum verification failed");
            
            var jsonSerializer = new DataContractJsonSerializer(typeof (SppCallbackContainer));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));

            return jsonSerializer.ReadObject(ms) as SppCallbackContainer;
        }

        private string Base64UrlDecode(string input)
        {
            input = input.Replace("-", "+").Replace("_", "/");
            input = input.PadRight(input.Length + (4 - input.Length % 4) % 4, '=');

            var sr = new StreamReader(new MemoryStream(Convert.FromBase64String(input)));
            return sr.ReadToEnd();
        }

        public OAuthToken GetServerToken(string clientId, string clientSecret)
        {
            var postValues = new Dictionary<string, string>
            {
                {"client_id", clientId},
                {"grant_type", "client_credentials"},
                {"client_secret", clientSecret},
            };

            var httpPostMessage = new HttpPost(_schibstedOAuthBaseUrl + "/token", postValues).GetString();

            var jsonSerializer = new DataContractJsonSerializer(typeof(OAuthToken));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(httpPostMessage));

            return jsonSerializer.ReadObject(ms) as OAuthToken;
        }

        public OAuthToken GetUserToken(string clientId, string clientSecret, string authCode)
        {
            var postValues = new Dictionary<string, string>
            {
                {"client_id", clientId},
                {"redirect_uri", HttpContext.Current.Request.Url.AbsoluteUri + "?authenticated=true"},
                {"grant_type", "authorization_code"},
                {"client_secret", clientSecret},
                {"code", authCode}
            };

            var httpPostMessage = new HttpPost(_schibstedOAuthBaseUrl + "/token", postValues).GetString();

            var jsonSerializer = new DataContractJsonSerializer(typeof(OAuthToken));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(httpPostMessage));

            return jsonSerializer.ReadObject(ms) as OAuthToken;
        }

        public string GetAuthorizeUrl(string clientId)
        {
            return string.Format(_schibstedOAuthBaseUrl + "authorize?client_id={0}&response_type=code&redirect_uri={1}",
                    clientId,
                    HttpContext.Current.Request.Url.AbsoluteUri + "?authenticated=true");
        }

        public string GetJsonStringFromOrderItems(IEnumerable<OrderItem> orderItems)
        {
            var orderItemDicts = new List<Dictionary<string, object>>();

            foreach (var orderItem in orderItems)
            {
                var dict = new Dictionary<string, object>()
                {
                    {"type", orderItem.Type},
                    {"description", orderItem.Description},
                    {"currency", orderItem.Currency},
                    {"price", orderItem.Price},
                    {"vat", orderItem.Vat},
                    {"quantity", orderItem.Quantity}
                };

                if (orderItem.Properties != null)
                {
                    foreach (var property in orderItem.Properties)
                    {
                        dict.Add(property.Key, property.Value);
                    }
                }

                orderItemDicts.Add(dict);
            }

            return GetJsonStringFromObject(orderItemDicts);
        }

        private string GetJsonStringFromObject(object objectData)
        {
            // Serializing object to JSON with the JavascriptSerializer, 
            // because .NET 4.0 doesn't include DataContractJsonSerializerSettings, 
            // which has the "simple dictionary format" setting.
            var jsSerializer = new JavaScriptSerializer();

            return jsSerializer.Serialize(objectData);
        }

        public PaymentStatus GetPaymentStatusFromOrderStatus(int status)
        {
            PaymentStatusCode paymentStatusCode;

            switch (status)
            {
                case -2:
                    paymentStatusCode = PaymentStatusCode.Cancelled; break;
                case -1:
                    paymentStatusCode = PaymentStatusCode.Declined; break;
                case 0:
                    paymentStatusCode = PaymentStatusCode.New; break;
                case 1:
                    paymentStatusCode = PaymentStatusCode.PendingAuthorization; break;
                case 2:
                    paymentStatusCode = PaymentStatusCode.Acquired; break;
                case 3:
                    paymentStatusCode = PaymentStatusCode.Refunded; break;
                case 4:
                    paymentStatusCode = PaymentStatusCode.Authorized; break;
                default:
                    paymentStatusCode = PaymentStatusCode.Declined; break;
            }

            return PaymentStatus.Get((int) paymentStatusCode);
        }
    }
}

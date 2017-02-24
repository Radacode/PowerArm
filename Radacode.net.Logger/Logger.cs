using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace radacode.net.logger
{
    public class Logger : ILogger
    {
        private const string _rdcNetUrl = "https://radacode.net";
        //private const string _rdcNetUrl = "http://rdc.net.api.local";

        private string _token;

        private string _login;
        private string _password;
        private string _instanceId;
        private string _clientId;

        public Logger(string login, string password, string instanceId, string clientId)
        {
            try
            {
                _clientId = clientId;

                var accessTokenAquirer = HttpPostUrlEncoded(
                    _rdcNetUrl + "/token",
                    new string[]
                    {
                        "grant_type",
                        "username",
                        "password",
                        "client_Id"
                    },
                    new string[]
                    {
                        "password",
                        login,
                        password,
                        clientId
                    });

                var data = JObject.Parse(accessTokenAquirer.Result);

                _token = (string)data.SelectToken("access_token");

                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                var res = client.GetAsync(_rdcNetUrl + "/api/auth/check").GetAwaiter().GetResult(); ;

                if(res.StatusCode != HttpStatusCode.OK)
                    throw new Exception("Unable to access RDC.NET with aquired token. Check call failed.");

                _instanceId = instanceId;

            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }

        public void Log(string message)
        {
            var payload = string.Empty;

            payload = JsonConvert.SerializeObject(
                new
                {
                    message = message,
                    time = DateTime.Now,
                    level = 0,
                    token = _instanceId
                });

            HttpPostJson(
                _rdcNetUrl + "/api/logs/add", payload,
                new Dictionary<string,string> { { "Authorization", "Bearer " +_token }} );
        }

        public void Error(string message, string stackTrace)
        {
            var payload = string.Empty;

            payload = JsonConvert.SerializeObject(
                new
                {
                    message = message,
                    time = DateTime.Now,
                    level = 2,
                    token = _instanceId,
                    stack = stackTrace
                });

            HttpPostJson(
                _rdcNetUrl + "/api/logs/add", payload,
                new Dictionary<string, string> { { "Authorization", "Bearer " + _token } });
        }

        static async Task<string> HttpPostJson(string url, string jsonPayload, Dictionary<string,string> headers)
        {
            HttpWebRequest req = WebRequest.Create(new Uri(url))
                as HttpWebRequest;
            req.Method = "POST";
            req.ContentType = "application/json";

            foreach (var header in headers)
            {
                req.Headers[header.Key] = header.Value;
            }
        
            // Encode the parameters as form data:
            byte[] formData =
                UTF8Encoding.UTF8.GetBytes(jsonPayload);

            if (req.Headers.AllKeys.Contains("Content-Length"))
            {
                req.Headers[HttpRequestHeader.ContentLength] = formData.Length.ToString();
            }

            // Send the request:
            using (Stream dataStream = await req.GetRequestStreamAsync())
            {
                dataStream.Write(formData, 0, formData.Length);
            }

            try
            {

                // Pick up the response:
                string result = null;
                using (HttpWebResponse resp = await req.GetResponseAsync()
                    as HttpWebResponse)
                {
                    StreamReader reader =
                        new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        static async Task<string> HttpPostUrlEncoded(string url, string[] paramName, string[] paramVal)
        {
            HttpWebRequest req = WebRequest.Create(new Uri(url))
                as HttpWebRequest;
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            StringBuilder paramz = new StringBuilder();
            for (int i = 0; i < paramName.Length; i++)
            {
                paramz.Append(paramName[i]);
                paramz.Append("=");
                paramz.Append(WebUtility.UrlEncode(paramVal[i]));
                if(i < (paramName.Length -1)) paramz.Append("&");
            }

            // Encode the parameters as form data:
            byte[] formData =
                UTF8Encoding.UTF8.GetBytes(paramz.ToString());

            if (req.Headers.AllKeys.Contains("Content-Length"))
            {
                req.Headers[HttpRequestHeader.ContentLength] = formData.Length.ToString();
            }

            // Send the request:
            using (Stream dataStream = await req.GetRequestStreamAsync())
            {
                dataStream.Write(formData, 0, formData.Length);
            }

            try
            {

                // Pick up the response:
                string result = null;
                using (HttpWebResponse resp = await req.GetResponseAsync()
                    as HttpWebResponse)
                {
                    StreamReader reader =
                        new StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
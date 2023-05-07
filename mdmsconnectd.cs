using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace iBotMDEgRPCAPICalling
{
    public static class mdmsconnectd
    {
        [FunctionName("mdmsconnectd")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "pkValue" , "tableName","accessKey"})]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string pkValue = req.Query["pkValue"];
            string tableName = req.Query["tableName"];
            string accessKey = req.Query["accessKey"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string msg = APICallingFunction("http://52.172.182.71:8055/items/" + tableName + "/" + pkValue + "?access_token=" + accessKey, requestBody, "PATCH");
            return new OkObjectResult(msg);
        }

        public static string APICallingFunction(string URL, string Message, string MethodName)
        {
            try
            {

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                //{"F02":"868345034811539","F03":"IBOT_Button","F04":"","F05":"","F06":"","F07":"","F08":"21","F09":"89011703278222063037","F11":"FC","F12":"78.376816,17.456806","F16":"124412,100519","F18":"1","F20":"Airtel","F21":"IOTHUBGSMBUTTONATT","F22":"4164","F23":"1"}
                //string URL2 = "https://connect.ibot.io/DataPost.svc/OTSConnectCall";
                string URL2 = URL;
                HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(URL2);
                webrequest.Method = "PATCH";
                //var serializedObject = "{\"clusterId\":\"7a5444cd-98bf-427e-bd0c-da26a96f0493\",\"version\":\"129\",\"privileged\":\"true\",\"network\":\"default\"}";
                var serializedObject = Message;//"{\"version\":\"129\"}";
                byte[] byteArray1 = Encoding.UTF8.GetBytes(serializedObject);
                //webrequest.Headers["authorization"] = "Bearer " + "hVlfUKgZkSaf3XMHbr3oxzyPKBZL16Nn";
                webrequest.ContentType = "application/json";//"Content-Encoding";
                webrequest.ContentLength = byteArray1.Length;
                Stream newStream1 = webrequest.GetRequestStream();
                newStream1.Write(byteArray1, 0, byteArray1.Length);
                newStream1.Close();
                HttpWebResponse webresponse4 = (HttpWebResponse)webrequest.GetResponse();
                Encoding enc4 = System.Text.Encoding.UTF8;
                StreamReader loResponseStream4 = new StreamReader(webresponse4.GetResponseStream(), enc4);
                string strResult = loResponseStream4.ReadToEnd();
                loResponseStream4.Close();
                webresponse4.Close();
                //MessgeLog("3000455", "Data sent successfully to Billing System: " + strResult);
                return strResult;

            }
            catch (Exception ex)
            {
                string error = ex.Message;
                //MessgeLog("3000455", "Failed sending data to Billing System: " + error);
                return "APIREQError: " + error;
            }
        }
    }
}


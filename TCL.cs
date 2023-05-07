using System;
using System.IO;
using System.Net;
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
using System.Data.SqlClient;
using Microsoft.Azure.Storage.Blob;

namespace iBotMDEgRPCAPICalling
{
    public static class TCL
    {
        private static byte[] filebytes;

        [FunctionName("TCL")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "deviceId" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "deviceId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/octet-stream", bodyType: typeof(string), Description = "The OK response")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string deviceId = req.Query["deviceId"];

            try
            {
                Microsoft.Azure.Storage.CloudStorageAccount storageAccount = Microsoft.Azure.Storage.CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=ibotstorageaccount;AccountKey=f7l9eYeSfPahvvsLPUr76JOZoE1K2a/4kF2RpKXwdLUYtpUnbc3uPRaoNq+iLEhWJ7DWrPXhJoT0BTwCbrPdug==;EndpointSuffix=core.windows.net");
                var connstr = "Server=tcp:hiveibotserver.database.windows.net,1433;Database=RegistryDB;User ID=hiveibotdmin;Password=Welcome1*;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";
                string FileName = "";
                //7f609470-62e9-11eb-b712-1f5e32fc2b35                
                if (deviceId != "")
                {
                    try
                    {
                        String strcontent = "";
                        string appRoot = Environment.GetEnvironmentVariable("RoleRoot");
                        //string uploadFolder = Path.Combine(appRoot + @"\", string.Format(@"approot\{0}", BotID + "1.0.0.1.hex"));
                        //string uploadFolder = Path.Combine(appRoot + @"\", string.Format(@"approot\{0}", "MQTT_FOTA_FILE.txt"));
                        string localFile = Path.Combine(context.FunctionAppDirectory, "FOTAFile");

                        using (SqlConnection cnn = new SqlConnection(connstr))
                        {
                            SqlCommand cmd = new SqlCommand("select * from fota_schedule where device_id='" + deviceId + "' and fota_open='true'", cnn);
                            if (cnn.State == System.Data.ConnectionState.Closed)
                            {
                                cnn.Open();
                            }
                            SqlDataReader dr = cmd.ExecuteReader();
                            if (dr.Read())
                            {
                                //string path2 = h("~");
                                //string uploadFolder = path2 + @"/Image/";
                                try
                                {
                                    FileName = dr["file_name"].ToString();
                                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                                    // Create the blob client.
                                    Microsoft.Azure.Storage.Blob.CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                                    // Retrieve reference to a previously created container.
                                    Microsoft.Azure.Storage.Blob.CloudBlobContainer container = blobClient.GetContainerReference("quickstartblobsdac5bd18-7ade-4ad8-ad15-be1dc5b914de");
                                    if (container.Exists())
                                    {
                                        // Retrieve reference to a blob named "photo1.jpg".
                                        Microsoft.Azure.Storage.Blob.CloudBlockBlob blockBlob = container.GetBlockBlobReference(FileName);
                                        await blockBlob.FetchAttributesAsync();

                                        var contentType = blockBlob.Properties.ContentType;
                                        var stream = new MemoryStream();
                                        await blockBlob.DownloadToStreamAsync(stream);
                                        var arrangementXMLString = Convert.ToBase64String(stream.ToArray());
                                        filebytes = Convert.FromBase64String(arrangementXMLString);
                                        return new FileContentResult(filebytes, contentType)
                                        {
                                            FileDownloadName = FileName
                                        };                                       
                                    }
                                }
                                catch (Exception ex)
                                {
                                    string error = ex.Message;
                                    //WebOperationContext.Current.OutgoingResponse.ContentType = "application/json; charset=utf-8";
                                    return new OkObjectResult(error);
                                }                                
                            }
                        }
                        new BadRequestObjectResult("{\"Error\":\"Bad Request\"}");
                    }
                    catch (Exception ex)
                    {
                        string error = "Error";
                        return new BadRequestObjectResult("{\"Error\":\"Bad Request\"}");
                    }
                }
                else
                {
                    string StrNoDevice = "Please enter device Id";

                    return new BadRequestObjectResult("{\"Error\":\"Bad Request\"}");
                }
            }
            catch (Exception ex)
            {
                string error = ex.Message;

                return new BadRequestObjectResult("{\"Error\":\"Bad Request\"}");
            }

            return new BadRequestObjectResult("{\"Error\":\"Bad Request\"}");
        }
    }
}


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
using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System;
using Grpc.Core;
using MDAS;
using System.Globalization;

namespace iBotMDEgRPCAPICalling
{
    public static class gRPCAPICallingFunction
    {
        [FunctionName("gRPCAPICallingFunction")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "deviceId" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "deviceId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string deviceId = req.Query["deviceId"];
            EventLog_Success("Recived request initiation process", deviceId);

            string Result = "";
            try
            {
                EventLog_Success("CallMDAS call from Run:" , deviceId);
                Result = CallMDAS(deviceId);
                EventLog_Success("CallMDAS call receive result:"+Result, deviceId);
            }
            catch (Exception ex)
            {
                EventLog_Success("Run-Exception:"+ex.Message, req.Query["deviceId"]);
                EventLogException_Error(ex.Message, req.Query["deviceId"]);
                //  throw;
            }
            
            var serializedObject = JsonConvert.SerializeObject(Result);

            EventLog_Success("Returned response:"+serializedObject.Replace("'",""), req.Query["deviceId"]);
            return new OkObjectResult(serializedObject);
        }

        public static string CallMDAS(string deviceId)
        {
            try
            {
                EventLog_Success("CallMDAS Process started",deviceId);
                string[] MeterInfo = deviceId.Split(';');
                if (MeterInfo[0].ToString() == "" && MeterInfo[1].ToString() != "rpcRestart")
                {
                    EventLog_Success("Please enter Meter Id", deviceId);
                    return "Please enter Meter Id";
                }                
                MDAS.Value value = new MDAS.Value
                {
                    Type = (MDAS.Value.Types.Type.Long)
                };
                if (MeterInfo.Length > 0)
                {
                 
                    Channel channel = new Channel("20.62.194.136:5001", ChannelCredentials.Insecure);
                    //Channel channel = new Channel("104.211.186.111:5001", ChannelCredentials.Insecure);
                    //Channel channel = new Channel("localhost:5001", ChannelCredentials.Insecure);
                    EventLog_Success("Channel Instaniation has done", deviceId);
                    var client = new MDAS.MDASServiceRPC.MDASServiceRPCClient(channel);
                    //We can add more requests here for other gRPC commands....
                    EventLog_Success("Client has created with the channel and process method : "+ MeterInfo[1].ToString(), deviceId);
                 
                    if (MeterInfo[1].ToString() == "rpcInstantaneousCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.InstantaneousSnapshotRequest
                        {
                            SerialNo = MeterInfo[0].ToString()//"KALKI_SM00000001"
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readInstantaneousSnapshotAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "InstantaneousSnapshotRequest request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcBlockProfileCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.BlockLoadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(), //"KALKI_SM00000001"
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID added
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readBlockLoadAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Block Load Profile request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteRealtimeclockCmd")
                    {
                        DateTime d = DateTime.ParseExact(MeterInfo[2].ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()))
                        {
                            byte[] values = BitConverter.GetBytes(long.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "timestamp",
                                Value = value,
                                TaskID = MeterInfo[3].ToString() // Additional param TaskID added
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Real Time Clock write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcBlockProfileRangeCmd")
                    {                        
                        
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && !string.IsNullOrEmpty(MeterInfo[3].ToString()))
                        {
                            byte[] values = BitConverter.GetBytes(0);
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request1 = new MDAS.BlockLoadRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),
                                FromTime = GetDateTimeForRpc(DateTime.ParseExact(MeterInfo[2].ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture)),
                                ToTime = GetDateTimeForRpc(DateTime.ParseExact(MeterInfo[3].ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture)),
                                TaskID = MeterInfo[4].ToString() // Additional param TaskID added
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.readBlockLoadAsync(request1);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Block Load Profile range request to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcDailyProfileCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.DailyLoadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(), //"KALKI_SM00000001"
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID added
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readDailyLoadAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Daily Load Profile request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcDailyProfileRangeCmd")
                    {                        
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && !string.IsNullOrEmpty(MeterInfo[3].ToString()) )
                        {
                            byte[] values = BitConverter.GetBytes(0);
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request1 = new MDAS.DailyLoadRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),
                                FromTime = GetDateTimeForRpc(DateTime.ParseExact(MeterInfo[1].ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture)),
                                ToTime = GetDateTimeForRpc(DateTime.ParseExact(MeterInfo[2].ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture)),
                                TaskID = MeterInfo[3].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.readDailyLoadAsync(request1);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Daily Load Profile range request to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcBillingProfileCmd")
                    {
                        var request1 = new MDAS.BillingProfileRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readBillingprofileAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Billing Load Profile request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcBillingProfileRangeCmd")
                    {
                        int intcheck1;
                        int intcheck2;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && !string.IsNullOrEmpty(MeterInfo[3].ToString()) && int.TryParse(MeterInfo[2].ToString(),out intcheck1) && int.TryParse(MeterInfo[3].ToString(), out intcheck2))
                        {
                            var request1 = new MDAS.BillingProfileRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                                FromEntry = int.Parse(MeterInfo[2].ToString()),
                                ToEntry = int.Parse(MeterInfo[3].ToString()),
                                TaskID = MeterInfo[4].ToString()// Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.readBillingprofileAsync(request1);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Billing Load Profile for selected entry request to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcVoltageRelatedEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.Voltage,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Voltage Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcCurrentRelatedEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.Current,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Current Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcDisconnectControlEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.DisconnectControl,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "DisconnectControl Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcNonRollOverEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.NonRollOver,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "NonRollOver Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcOtherEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.Other,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Other Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcPowerEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.Power,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Power Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcTransactionEventLogCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.EventLogRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001"
                            EventType = MDAS.EventLogRequest.Types.EventType.Transaction,
                            TaskID = MeterInfo[2].ToString()// Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readEventLogAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Transaction Event Log request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcNamePlateParamsRequestCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request1 = new MDAS.NamePlateParamsRequest
                        {
                            SerialNo = MeterInfo[0].ToString(), //"KALKI_SM00000001"
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readNameplateparamsSnapshotAsync(request1);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Nameplate Parameters request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcConnectLoadCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Disconnect_Control_Remote_Reconnect",
                            Value = value,
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Connect Load Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetRealtimeclockCmd")
                    {
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "timestamp",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Real Time Clock request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }                    
                    else if (MeterInfo[1].ToString() == "rpcGetDemandIntegrationPeriodCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Demand_Integration_Period",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Demand Integration Period request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteDemandIntegrationPeriodCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "Demand_Integration_Period",
                                Value = value,
                                TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Demand Integration Period write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetProfileCapturePeriodCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Profile_Capture_Period",
                            TaskIDTaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Profile Capture Period request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteProfileCapturePeriodCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(0);
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "Profile_Capture_Period",
                                TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Profile Capture Period write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }                         
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetSingleactionScheduleforBillingDatesCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Single_action_Schedule",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Single Action Schedule request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteSingleactionScheduleforBillingDatesCmd")
                    {
                        DateTime datecheck1;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && DateTime.TryParse(DateTime.ParseExact(MeterInfo[2].ToString(), "HHmmss,ddMMyy", System.Globalization.CultureInfo.InvariantCulture).ToString(), out datecheck1))
                        {
                            DateTime BillingDate = DateTime.ParseExact(MeterInfo[2].ToString(), "HHmmss,ddMMyy", System.Globalization.CultureInfo.InvariantCulture);
                            for (int i = 0; i < 100; i++)
                            {
                                MDAS.SingleAction singleAction1 = new MDAS.SingleAction();
                                singleAction1.Date = new MDAS.Date();
                                singleAction1.Time = new MDAS.Time();
                                singleAction1.Date.Year = BillingDate.Year;
                                singleAction1.Date.Month = BillingDate.Month;
                                singleAction1.Date.Day = BillingDate.Day;
                                singleAction1.Time.Hour = BillingDate.Hour;
                                singleAction1.Time.Minute = BillingDate.Minute;
                                singleAction1.Time.Second = BillingDate.Second;
                                value.Singleactions.Add(singleAction1);
                            }
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),
                                ParamName = "Single_action_Schedule",
                                Value = value,
                                TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Single Action Schedule write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetActivityCalendarSeasonProfilePassiveCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Activity_Calendar_Season_Profile_passive",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Active Calendar Season Profile passive request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteActivityCalendarSeasonProfilePassiveCmd")
                    {                        
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()))
                        {
                            MDAS.SeasonProfile seasonProfile1 = new MDAS.SeasonProfile();
                            seasonProfile1.SeasonName = "Summer";
                            seasonProfile1.SeasonStart = new MDAS.Date_Time();
                            seasonProfile1.SeasonStart.Year = 2021;
                            seasonProfile1.SeasonStart.Month = 2;
                            seasonProfile1.SeasonStart.Date = 5;
                            value.Seasonprofiles.Add(seasonProfile1);
                            for (int i = 0; i < 20; i++)
                            {
                                MDAS.SeasonProfile seasonProfile2 = new MDAS.SeasonProfile();
                                seasonProfile2.SeasonName = "Winter";
                                seasonProfile2.SeasonStart = new MDAS.Date_Time();
                                seasonProfile2.SeasonStart.Year = 2021;
                                seasonProfile2.SeasonStart.Month = 1;
                                seasonProfile2.SeasonStart.Date = 1;
                                value.Seasonprofiles.Add(seasonProfile2);
                            }
                            //byte[] values = BitConverter.GetBytes(0);
                            //value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),
                                ParamName = "Activity_Calendar_Season_Profile_passive",
                                Value = value,
                                TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Active Calendar Season Profile passive write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetActivityCalendarWeekProfilepassiveCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Activity_Calendar_Week_Profile_passive",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Activity Calendar Week Profile passive request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteActivityCalendarWeekProfilepassiveCmd")
                    {
                        MDAS.WeekProfile weekProfile1 = new MDAS.WeekProfile();
                        weekProfile1.WeekName = "w1";
                        weekProfile1.Sunday = 1;
                        weekProfile1.Monday = 2;
                        weekProfile1.Tuesday = 3;
                        weekProfile1.Wednesday = 4;
                        weekProfile1.Thursday = 5;
                        weekProfile1.Friday = 6;
                        weekProfile1.Saturday = 7;
                        value.Weekprofiles.Add(weekProfile1);
                        for (int i = 0; i < 30; i++)
                        {
                            MDAS.WeekProfile weekProfile2 = new MDAS.WeekProfile();
                            weekProfile2.WeekName = "w1";
                            weekProfile2.Sunday = 1;
                            weekProfile2.Monday = 2;
                            weekProfile2.Tuesday = 3;
                            weekProfile2.Wednesday = 4;
                            weekProfile2.Thursday = 5;
                            weekProfile2.Friday = 6;
                            weekProfile2.Saturday = 7;
                            value.Weekprofiles.Add(weekProfile2);
                        }
                        //byte[] values = BitConverter.GetBytes(0);
                        //value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),
                            ParamName = "Activity_Calendar_Week_Profile_passive",
                            Value = value,
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Activity Calendar Week Profile passive write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetActivityCalendarPassivecalendaractivationdatetimeCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "AActivity_Calendar_Passive_calendar_activation_datetime",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Activity Calendar Passive calendar activation date time request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetActivityCalendarDayProfilepassiveCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Activity_Calendar_Day_Profile_passive",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Activity Calendar Day Profile passive request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteActivityCalendarDayProfilepassiveCmd")
                    {
                        MDAS.DayProfile[] dayProfiles = new MDAS.DayProfile[2];
                        dayProfiles[0] = new MDAS.DayProfile();
                        dayProfiles[0].Dayid = 1;
                        MDAS.DayAction dayAction = new MDAS.DayAction();
                        dayAction.ScriptSelector = 1;
                        MDAS.Time time = new MDAS.Time();
                        time.Hour = 1;
                        time.Minute = 30;
                        time.Second = 45;
                        dayAction.StartTime = new MDAS.Time(time);
                        dayProfiles[0].DayActions.Add(dayAction);
                        dayProfiles[1] = new MDAS.DayProfile();
                        dayProfiles[1].Dayid = 3;
                        MDAS.DayAction dayAction2;
                        for (int i = 0; i < 400; i++)
                        {
                            dayAction2 = new MDAS.DayAction();
                            dayAction2.ScriptSelector = 1;
                            dayAction2.StartTime = new MDAS.Time();
                            dayAction2.StartTime.Hour = 12;
                            dayAction2.StartTime.Minute = 45;
                            dayAction2.StartTime.Second = 13;
                            dayProfiles[1].DayActions.Add(dayAction2);
                        }
                        value.DayProfiles.Add(dayProfiles);
                        //byte[] values = BitConverter.GetBytes(0);
                        //value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),
                            ParamName = "Activity_Calendar_Day_Profile_passive",
                            Value = value, 
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Activity Calendar Day Profile passive write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetActivityCalendarPassiveCalendarActivateMethodCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Activity_Calendar_Passive_calendar_activate_method",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Activity Calendar Passive calendar activate method request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetLoadlimitactiveCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Load_limit_active",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Load limit active request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteLoadlimitactiveCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "Load_limit_active",
                                Value = value,
                                TaskID = MeterInfo[3].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Load limit active write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetLoadlimitNormalCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Load_limit_normal",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Load limit normal request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteLoadlimitNormalCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "Load_limit_normal",
                                Value = value,
                                TaskID = MeterInfo[3].ToString() // Additional param TaskID 
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Load limit normal write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetMeteringModeCmd")
                    {
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Metering_Mode",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID 
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Metering Mode request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteMeteringModeCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "Metering_Mode",
                                Value = value,
                                TaskID = MeterInfo[3].ToString() // Additional param TaskID
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Metering Mode write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetPaymentModeCmd")
                    {
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Payment_mode",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Payment Mode request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWritePaymentModeCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "Payment_mode",
                                Value = value,
                                TaskID = MeterInfo[3].ToString()  // Additional param TaskID
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "Payment Mode write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please Check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetLastTokenRechargeAmountCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Last_token_recharge_amount",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Last token recharge amount request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetLastTokenRechargeTimeCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Last_token_recharge_time",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Last token recharge time request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetTotalAmountatLastRechargeCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Total_amount_at_last_recharge",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Total amount at last recharge request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetCurrentBalanceAmountCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Current_balance_amount",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Current balance amount request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetLLSsecretCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "LLS_secret",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "LLS secret request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetHLSkeyCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "HLS_key",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "HLS key request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetGlobalKeyChangeCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Global_key_change",
                            TaskID = MeterInfo[2].ToString() // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Global key change request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetImageActivationSingleActionScheduleCmd")
                    {
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Image_activation_single_action_schedule",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Image activation single action schedule request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetESWFCmd")
                    {
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "ESWF",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "ESWF request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteESWFCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "ESWF",
                                Value = value,
                                TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "ESWF write request sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please Check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetMDResetCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "MD_Reset",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "MD Reset sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteMDResetCmd")
                    {
                        int checkInt;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && int.TryParse(MeterInfo[2].ToString(), out checkInt))
                        {
                            byte[] values = BitConverter.GetBytes(int.Parse(MeterInfo[2].ToString()));
                            value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                            var request2 = new MDAS.ProgrammableParamsWriteRequest
                            {
                                SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                                ParamName = "MD_Reset",
                                Value = value,
                                TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                            };
                            AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request2);
                            response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                            //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                            return "MD Reset write sent to MDE is Success for meter " + MeterInfo[0].ToString();
                        }
                        else
                        {
                            return "!Please Check, its invalid input";
                        }
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetDisconnectControlRemoteDisconnectCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Disconnect_Control_Remote_Disconnect",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Disconnect Control Remote Disconnect sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcGetDisconnectControlRemoteReconnectCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Disconnect_Control_Remote_Reconnect",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Disconnect Control Remote Reconnect sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcWriteRealTimeClockCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request2 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Disconnect_Control_Remote_Reconnect",
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.readProgrammableParamsAsync(request2);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Disconnect Control Remote Reconnect sent to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcRestart")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request4 = new MDAS.restartRequest
                        { };
                        AsyncUnaryCall<MDAS.Reply> response = client.restartMDASEngineAsync(request4);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "MDAS Engine Restarted";
                    }
                    else if (MeterInfo[1].ToString() == "rpcGatewayPrefixChange")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request5 = new MDAS.ConfigurationUpdateRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),
                            Value = MeterInfo[2].ToString(),
                            ConfigurationType = MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.GatewayPrefixUpdate
                            
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.notifyConfigurationChangeAsync(request5);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Gateway change notification Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcMeterAdd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request6 = new MDAS.ConfigurationUpdateRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),
                            Value = MeterInfo[2].ToString(),
                            ConfigurationType = MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.MeterAdd

                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.notifyConfigurationChangeAsync(request6);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Meter add notification Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcMeterDelete")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request7 = new MDAS.ConfigurationUpdateRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),
                            Value = MeterInfo[2].ToString(),
                            ConfigurationType = MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.MeterDelete
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.notifyConfigurationChangeAsync(request7);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Meter Delete notification Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcDisconnectLoadCmd")
                    {
                        byte[] values = BitConverter.GetBytes(0);
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));
                        var request3 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Disconnect_Control_Remote_Disconnect",
                            Value = value,
                            TaskID = MeterInfo[2].ToString()
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request3);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        EventLog_Success("rpcDisconnectLoadCmd : " + MeterInfo[1].ToString(), deviceId);
                        return "Disconnect Load Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcRechargeCmd")
                    {
                        long longcheck;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && MeterInfo[2].ToString() == "0" && !long.TryParse(MeterInfo[2].ToString(), out longcheck))
                        {
                            return "Please enter recharge amount";
                        }
                        byte[] values = new byte[8];
                        values = BitConverter.GetBytes(long.Parse(MeterInfo[2].ToString()));
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));

                        System.Threading.Thread.Sleep(2);
                        //calling of Read programmable parameter for the current meter
                       /*
                        var request9 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Current_balance_amount",
                            //Value = value
                        };
                        AsyncUnaryCall<MDAS.Reply> response2 = client.readProgrammableParamsAsync(request9);
                        response2.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //calling of Read programmable parameter for the current meter
                        var request10 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Current_balance_time",
                            //Value = value
                        };
                        AsyncUnaryCall<MDAS.Reply> response3 = client.readProgrammableParamsAsync(request10);
                        response3.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //calling of Read programmable parameter for the current meter
                        var request11 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Last_token_recharge_amount",
                            //Value = value
                        };
                        AsyncUnaryCall<MDAS.Reply> response4 = client.readProgrammableParamsAsync(request11);
                        response4.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //calling of Read programmable parameter for the current meter
                        var request12 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Last_token_recharge_time",
                            //Value = value
                        };
                        AsyncUnaryCall<MDAS.Reply> response5 = client.readProgrammableParamsAsync(request12);
                        response5.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //calling of Read programmable parameter for the current meter
                        var request14 = new MDAS.ProgrammableParamsReadRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Total_amount_at_last_recharge",
                            //Value = value
                        };
                        AsyncUnaryCall<MDAS.Reply> response6 = client.readProgrammableParamsAsync(request14);
                        response6.ResponseAsync.ContinueWith(EventLogRequestFinished);
                       */
                        var request8 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Last_token_recharge_amount",
                            Value = value,
                            TaskID = MeterInfo[3].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response = client.writeProgrammableParamsAsync(request8);
                        response.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        //response.ResponseAsync.ContinueWith(task => Console.Write(response.ResponseAsync.Result.Message));
                        return "Recharge request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcCurrentBalanceUpdateCmd")
                    {
                        long longcheck;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && MeterInfo[2].ToString() == "0" && !long.TryParse(MeterInfo[2].ToString(), out longcheck))
                        {
                            return "Please enter current balance recharge amount";
                        }
                        byte[] values = new byte[8];
                        values = BitConverter.GetBytes(long.Parse(MeterInfo[2].ToString()));
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));

                        System.Threading.Thread.Sleep(2);
                        //calling of Read programmable parameter for the current meter
                        var request9 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "Current_balance_amount",
                            Value = value,
                            TaskID = MeterInfo[3].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response2 = client.writeProgrammableParamsAsync(request9);
                        response2.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        return "Current Balance amount update request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcUpdateLLSsecretCmd")
                    {
                        long longcheck;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && MeterInfo[2].ToString() == "0" && !long.TryParse(MeterInfo[2].ToString(),out longcheck))
                        {
                            return "Please enter valid LLS secret";
                        }
                        byte[] values = new byte[8];
                        values = BitConverter.GetBytes(long.Parse(MeterInfo[2].ToString()));
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));

                        System.Threading.Thread.Sleep(2);
                        //calling of Read programmable parameter for the current meter
                        var request9 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "LLS_secret",
                            Value = value,
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response2 = client.writeProgrammableParamsAsync(request9);
                        response2.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        return "LLS secret update request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else if (MeterInfo[1].ToString() == "rpcUpdateHLSkeyCmd")
                    {
                        long longcheck;
                        if (!string.IsNullOrEmpty(MeterInfo[2].ToString()) && MeterInfo[2].ToString() == "0" && !long.TryParse(MeterInfo[2].ToString(), out longcheck))
                        {
                            return "Please enter valid LLS secret";
                        }
                        byte[] values = new byte[8];
                        values = BitConverter.GetBytes(long.Parse(MeterInfo[2].ToString()));
                        value.Values.Add(Google.Protobuf.ByteString.CopyFrom(values));

                        System.Threading.Thread.Sleep(2);
                        //calling of Read programmable parameter for the current meter
                        var request9 = new MDAS.ProgrammableParamsWriteRequest
                        {
                            SerialNo = MeterInfo[0].ToString(),//"KALKI_SM00000001",
                            ParamName = "HLS_key",
                            Value = value,
                            TaskID = MeterInfo[2].ToString()  // Additional param TaskID
                        };
                        AsyncUnaryCall<MDAS.Reply> response2 = client.writeProgrammableParamsAsync(request9);
                        response2.ResponseAsync.ContinueWith(EventLogRequestFinished);
                        return "HLS key update request to MDE is Success for meter " + MeterInfo[0].ToString();
                    }
                    else
                    {
                        EventLog_Success("Call Dams Error : Invalid input(method not found)", deviceId);
                        EventLogException_Error("Invalid input(method not found) ", deviceId);
                        return "Invalid input " + deviceId;
                    }
                }
                else
                {
                    EventLog_Success("Call Dams Error : Invalid input " + deviceId, deviceId);
                    EventLogException_Error("Invalid input " + deviceId, deviceId);
                    return "Invalid input " + deviceId;
                }
            }
            catch (Exception ex)
            {
                string Error = ex.Message;
                EventLog_Success("Call Dams exception : " + ex.Message, deviceId);
                EventLogException_Error(Error, deviceId);

                return Error;
            }
        }

        static MDAS.Date_Time GetDateTimeForRpc(DateTime val)
        {
            MDAS.Date_Time grpcval = new MDAS.Date_Time();
            grpcval.Date = val.Day;
            grpcval.Month = val.Month;
            grpcval.Year = val.Year;
            grpcval.Hour = val.Hour;
            grpcval.Minute = val.Minute;
            grpcval.Second = val.Second;
            return grpcval;
        }        

        static string configuration = "";

        static MDAS.ConfigurationUpdateRequest.Types.ConfigurationType GetConfigurationType()
        {
            if (configuration == "GatewayPrefixChange")
            {
                return MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.GatewayPrefixUpdate;
            }
            else if (configuration == "MeterAdd")
            {
                return MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.MeterAdd;
            }
            else if (configuration == "MeterDelete")
            {
                return MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.MeterDelete;
            }
            return MDAS.ConfigurationUpdateRequest.Types.ConfigurationType.MeterAdd;
        }

        [FunctionName("NestleApolloGetAsset")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "deviceId" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "deviceId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public static async Task<IActionResult> Run2([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var connstr = "Server=tcp:hiveibotserver.database.windows.net,1433;Database=RegistryDB;User ID=hiveibotdmin;Password=Welcome1*;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";
            string deviceId = req.Query["deviceId"];

            AssetInfo astInfo = new AssetInfo();
            string result = "";
            string AssetID = "";
            try
            {
                //EncryptAndDecrypt EC = new EncryptAndDecrypt();
                using (SqlConnection cnn9 = new SqlConnection(connstr))
                {
                    SqlDataAdapter da = new SqlDataAdapter("select * from ApolloAssetInfo where deviceId='" + deviceId + "'", cnn9);
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        astInfo.deviceId = deviceId;
                        astInfo.apolloId = ds.Tables[0].Rows[0]["apolloId"].ToString();
                        astInfo.assetId = ds.Tables[0].Rows[0]["assetId"].ToString();
                        astInfo.fileLink = ds.Tables[0].Rows[0]["assetFileLink"].ToString(); ;
                        var serializedObject = JsonConvert.SerializeObject(astInfo);
                        result = serializedObject;
                    }
                    else
                    {
                        SqlCommand cmd = new SqlCommand("select max(apolloId) from ApolloAssetInfo", cnn9);
                        if (cnn9.State == ConnectionState.Closed)
                        {
                            cnn9.Open();
                        }
                        AssetID = cmd.ExecuteScalar().ToString();
                        if (AssetID != null && AssetID != "")
                        {
                            AssetID = AssetID.Substring(6);
                            int s = Convert.ToInt32(AssetID) + 1;
                            AssetID = s.ToString();
                            while (AssetID.Length < 5)
                            {
                                AssetID = "0" + AssetID;
                            };
                            AssetID = "APOLLO" + AssetID;
                        }
                        else
                        {
                            AssetID = "APOLLO00001";
                        }
                        cmd = new SqlCommand("insert into ApolloAssetInfo(deviceId,apolloId,createdDate,assetFileLink) values('" + deviceId + "','" + AssetID + "','" + DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss") + "','iBotFOTA@104.211.140.26/assetDeploy/Planogram_PeruGTM_1.0_22022021.zip')", cnn9);
                        if (cnn9.State == ConnectionState.Closed)
                        {
                            cnn9.Open();
                        }
                        int S = cmd.ExecuteNonQuery();
                        if (S > 0)
                        {
                            astInfo.deviceId = deviceId;
                            astInfo.apolloId = AssetID;
                            astInfo.assetId = "";
                            astInfo.fileLink = "iBotFOTA@104.211.140.26/assetDeploy/Planogram_PeruGTM_1.0_22022021.zip";
                            var serializedObject = JsonConvert.SerializeObject(astInfo);
                            result = serializedObject;
                        }
                        else
                        {
                            new BadRequestObjectResult("{\"Error\":\"Bad Request\"}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "Bad Request")
                {
                    new BadRequestObjectResult("{\"Error\":\"" + ex.Message + "\"}");
                }
                else
                {
                    result = "{\"Error\":\"" + ex.Message + "\"}";
                }
            }
            return new OkObjectResult(result);
        }

        static void EventLogRequestFinished(Task<Reply> reply)
          {

            EventLog_Success("EventLogRequestFinished started :  SerialNo:"+ reply.Result.SerialNo.ToString()+"status:"+ reply.Result.Status.ToString(), reply.Result.SerialNo.ToString()+";"+reply.Result.TaskID);
            MySqlConnection con = null;
            MySqlDataReader reader = null;         
            try
                    {
                String connection_string = @"server=20.62.194.136;port=3306;database=MDMS;userid=root;password=admin@123;";
                con = new MySqlConnection(connection_string);               
                con.Open();
                string CommandText = "";
                if (reply.Status != TaskStatus.Faulted)
                {

                    if (reply.Result.Status.ToString().ToLower() == "finished")
                    {
                        CommandText = "update recharge_history_mdas set status = 'Closed', mdestatus = 'Finished', mde_status_updated_time = NOW() where recharge_id = '" + reply.Result.TaskID + "'";
                    }
                    else if (reply.Result.Status.ToString().ToLower() == "failed")
                    {
                        CommandText = "update recharge_history_mdas set mdestatus = 'Failed', mde_status_updated_time = NOW() where recharge_id = '" + reply.Result.TaskID + "'";
                    }
                    else
                    {
                        CommandText = "insert into errors(message,query_type,created_at) values('" + reply.Result.Status.ToString() + "','" + reply.Result.TaskID + "',NOW())";
                    }
                }
                else
                {
                    CommandText = "update recharge_history_mdas set status = 'Faulted', mdestatus = 'Faulted', mde_status_updated_time = NOW() where recharge_id = '" + reply.Result.TaskID + "'";
                }
                MySqlCommand cmd = new MySqlCommand(CommandText, con);
                int k = cmd.ExecuteNonQuery();
                Console.WriteLine("Updated");
                EventLog_Success("EventLogRequestFinished finished(updated success) :  SerialNo:" + reply.Result.SerialNo.ToString() + "status:" + reply.Result.Status.ToString(), reply.Result.SerialNo.ToString() + ";" + reply.Result.TaskID);

            }
            catch (Exception ex)
            {
                EventLog_Success("EventLogRequestFinished exception :  SerialNo:" + reply.Result.SerialNo.ToString() + "status:" + reply.Result.Status.ToString(), reply.Result.SerialNo.ToString() + ";" + reply.Result.TaskID);

                Console.WriteLine(ex);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                }
            }
        }
        static void EventLogException_Error(string msg,string querytype)
        {
            MySqlConnection con = null;
       
            try
            {
                String connection_string = @"server=20.62.194.136;port=3306;database=MDMS;userid=root;password=admin@123;";
                con = new MySqlConnection(connection_string);
                con.Open();
                string CommandText = "insert into errors(message,query_type,created_at) values('"+msg+"','"+ querytype + "',NOW())";
               
                MySqlCommand cmd = new MySqlCommand(CommandText, con);
                int k = cmd.ExecuteNonQuery();
                Console.WriteLine("inserted");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                }
            }
        }

        static void EventLog_Success(string msg, string deviceID)
        {
            MySqlConnection con = null;

            try
            {
                String connection_string = @"server=20.62.194.136;port=3306;database=MDMS;userid=root;password=admin@123;";
                con = new MySqlConnection(connection_string);
                con.Open();
                string CommandText = "insert into grpc_errors(device_id,message,created_date_time) values('" + deviceID + "','" + msg + "',NOW())";

                MySqlCommand cmd = new MySqlCommand(CommandText, con);
                int k = cmd.ExecuteNonQuery();
                Console.WriteLine("inserted");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                }
            }
        }
    }
}


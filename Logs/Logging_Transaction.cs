using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;

namespace iBotMDEgRPCAPICalling.Logs
{
    public class Logging_Transaction
    {
        //static SqlConnection mssqlcnn = new SqlConnection(ConfigurationManager.ConnectionStrings["MSSQlMDASConnection"].ToString());
        public Logging_Transaction(string MeterID, string Msg)
        {
            MessgeLog(MeterID, Msg);
        }

        public static void MessgeLog(string MeterID, string Msg)
        {   
            try
            {
                var mssqlcnnstr = "Server=tcp:ibotmde.database.windows.net,1433;Initial Catalog=mdasdb;Persist Security Info=False;User ID=iBotMDE_SQL;Password=admin@06012021;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
                using (SqlConnection mssqlcnn = new SqlConnection(mssqlcnnstr))
                {
                    SqlCommand cmd1 = new SqlCommand(@"insert into T1034(F02,F03,F04) values('" + MeterID + "','" + Msg + "','" + DateTime.UtcNow.ToString("MM-dd-yyyy HH:mm:ss") + "')", mssqlcnn);
                    if (mssqlcnn.State == ConnectionState.Closed)
                    {
                        mssqlcnn.Open();
                    }
                    int k = cmd1.ExecuteNonQuery();
                    mssqlcnn.Close();
                }                
            }
            catch (Exception ex)
            {
                string Error = ex.Message;
            }            
        }
    }
}

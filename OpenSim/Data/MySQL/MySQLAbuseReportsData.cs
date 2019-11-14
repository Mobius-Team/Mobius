using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;
using System.Reflection;

namespace OpenSim.Data.MySQL
{
    public class MySqlAbuseReportsData : MySQLGenericTableHandler<AbuseReportData>, IAbuseReportsData
    {
        public MySqlAbuseReportsData(string connectionString)
                : base(connectionString, "AbuseReports", "AbuseReports")
        {
        }


        public override bool Store(AbuseReportData row)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                string query = "";
                List<String> names = new List<String>();
                List<String> values = new List<String>();

                foreach (FieldInfo fi in m_Fields.Values)
                {
                    names.Add(fi.Name);
                    values.Add("?" + fi.Name);

                    if (fi.GetValue(row) == null)
                        throw new NullReferenceException(
                            string.Format(
                                "[MYSQL GENERIC TABLE HANDLER]: Trying to store field {0} for {1} which is unexpectedly null",
                                fi.Name, row));

                    if(fi.Name == "ImageData")
                        cmd.Parameters.Add("ImageData", MySqlDbType.Blob).Value = fi.GetValue(row);
                    else
                        cmd.Parameters.AddWithValue(fi.Name, fi.GetValue(row).ToString());
                }

                query = String.Format("replace into {0} (`", m_Realm) + String.Join("`,`", names.ToArray()) + "`) values (" + String.Join(",", values.ToArray()) + ")";

                cmd.CommandText = query;

                if (ExecuteNonQuery(cmd) > 0)
                    return true;

                return false;
            }
        }
    }
}

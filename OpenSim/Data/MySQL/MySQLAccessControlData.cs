using System.Reflection;
using System.Data;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySqlAccessControlData : MySqlFramework, IAccessControlData
    {
        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlAccessControlData(string connectionString)
                : base(connectionString)
        {
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "AccessControl");
                m.Update();
                dbcon.Close();
            }
        }

        public bool IsHardwareBanned(string mac, string id0)
        {
            bool is_banned = false;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("select mac as id from `banned_macs` where mac = ?mac UNION ALL select id0 as id from `banned_id0s` where id0 = ?id0 LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?mac", mac);
                    cmd.Parameters.AddWithValue("?id0", id0);

                    using(IDataReader result = cmd.ExecuteReader())
                    {
                        if(result.Read())
                        {
                            dbcon.Close();
                            is_banned = true;
                        }
                        else
                        {
                            dbcon.Close();
                        }
                    }
                }
            }
            
            return is_banned;
        }

        public bool IsIPBanned(string ip)
        {
            bool is_banned = false;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("select * from `banned_ips` where ip = ?ip LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?ip", ip);

                    using(IDataReader result = cmd.ExecuteReader())
                    {
                        if(result.Read())
                        {
                            dbcon.Close();
                            is_banned = true;
                        }
                        else
                        {
                            dbcon.Close();
                        }
                    }
                }
            }
            
            return is_banned;
        }
    }
}
using System;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

namespace OpenSim.Services.AbuseReportsService
{
    public class AbuseReportsServiceBase : ServiceBase
    {
        protected IAbuseReportsData m_Database = null;

        public AbuseReportsServiceBase(IConfigSource config)
            : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;
            string realm = "AbuseReports";

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // [AbuseReportsService] section overrides [DatabaseService], if it exists
            //
            IConfig presenceConfig = config.Configs["AbuseReportsService"];
            if (presenceConfig != null)
            {
                dllName = presenceConfig.GetString("StorageProvider", dllName);
                connString = presenceConfig.GetString("ConnectionString", connString);
                realm = presenceConfig.GetString("Realm", realm);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName.Equals(String.Empty))
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IAbuseReportsData>(dllName, new Object[] { connString });
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module " + dllName);

        }
    }
}

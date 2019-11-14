using System;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.AbuseReports
{
    public class AbuseReportsServiceConnector : ServiceConnector
    {
        private IAbuseReportsService m_AbuseReportsService;
        private string m_ConfigName = "AbuseReportsService";

        public AbuseReportsServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string service = serverConfig.GetString("LocalServiceModule", String.Empty);

            if (service == String.Empty)
                throw new Exception("LocalServiceModule not present in AbuseReportsService config file AbuseReportsService section");

            Object[] args = new Object[] { config };
            m_AbuseReportsService = ServerUtils.LoadPlugin<IAbuseReportsService>(service, args);

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);

            server.AddStreamHandler(new AbuseReportsServerPostHandler(m_AbuseReportsService, auth));
        }
    }
}

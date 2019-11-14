using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.AbuseReportsService
{
    public class AbuseReportsService : AbuseReportsServiceBase, IAbuseReportsService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public AbuseReportsService(IConfigSource config)
            : base(config)
        {
            m_log.Debug("[ABUSE REPORTS SERVICE]: Starting abuse reports service");
        }

        public bool ReportAbuse(AbuseReportData report)
        {
			if (!m_Database.Store(report))
				return false;

            return true;
        }
    }
}

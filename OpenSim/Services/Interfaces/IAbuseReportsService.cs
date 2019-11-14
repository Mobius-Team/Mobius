using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IAbuseReportsService
    {
        bool ReportAbuse(AbuseReportData report);
    }
}

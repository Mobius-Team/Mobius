using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public interface IAbuseReportsData
    {
        bool Store(AbuseReportData data);
    }
}

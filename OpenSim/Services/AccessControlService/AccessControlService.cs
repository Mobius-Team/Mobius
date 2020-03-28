using OpenSim.Services.Interfaces;
using log4net;
using Nini.Config;
using System.Reflection;

namespace OpenSim.Services.AccessControlService
{
    public class AccessControlService :
            AccessControlServiceBase, IAccessControlService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public AccessControlService(IConfigSource config) :
                base(config)
        {
            
        }
    }
}

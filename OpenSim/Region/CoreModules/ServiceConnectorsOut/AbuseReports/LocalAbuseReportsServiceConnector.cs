using log4net;
using Mono.Addins;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.AbuseReports
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalAbuseReportsServicesConnector")]
    public class LocalAbuseReportsServicesConnector : ISharedRegionModule, IAbuseReportsService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_Scenes = new List<Scene>();
        protected IAbuseReportsService m_service = null;

        private bool m_Enabled = false;

         #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalAbuseReportsServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            // only active for core mute lists module
            IConfig moduleConfig = source.Configs["Messaging"];
            if (moduleConfig == null)
                return;

            if (moduleConfig.GetString("AbuseReportsModule", "None") != "AbuseReportsModule")
                return;

            moduleConfig = source.Configs["Modules"];

            if (moduleConfig == null)
                return;

            string name = moduleConfig.GetString("AbuseReportsService", "");
            if(name != Name)
                return;

            IConfig userConfig = source.Configs["AbuseReportsService"];
            if (userConfig == null)
            {
                m_log.Error("[ABUSE REPORTS LOCALCONNECTOR]: AbuseReportsService missing from configuration");
                return;
            }

            string serviceDll = userConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (serviceDll == String.Empty)
            {
                m_log.Error("[ABUSE REPORTS LOCALCONNECTOR]: No LocalServiceModule named in section AbuseReportsService");
                return;
            }

            Object[] args = new Object[] { source };
            try
            {
                m_service = ServerUtils.LoadPlugin<IAbuseReportsService>(serviceDll, args);
            }
            catch
            {
                m_log.Error("[ABUSE REPORTS LOCALCONNECTOR]: Failed to load mute service");
                return;
            }

            if (m_service == null)
            {
                m_log.Error("[ABUSE REPORTS LOCALCONNECTOR]: Can't load MuteList service");
                return;
            }

            m_Enabled = true;
            m_log.Info("[ABUSE REPORTS LOCALCONNECTOR]: enabled");
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock(m_Scenes)
            {
                m_Scenes.Add(scene);
                scene.RegisterModuleInterface<IAbuseReportsService>(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock(m_Scenes)
            {
                if (m_Scenes.Contains(scene))
                {
                    m_Scenes.Remove(scene);
                    scene.UnregisterModuleInterface<IAbuseReportsService>(this);
                }
            }
        }

        #endregion ISharedRegionModule

        #region IAbuseReportsService
        public bool ReportAbuse(AbuseReportData report)
        {
            if (!m_Enabled)
                return false;
            return m_service.ReportAbuse(report);
        }
        #endregion IAbuseReportsService
    }
}

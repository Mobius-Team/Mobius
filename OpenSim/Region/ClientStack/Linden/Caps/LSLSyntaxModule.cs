using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System;
using System.IO;
using System.Reflection;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LSLSyntax")]
    public class LSLSyntaxModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;
        private UUID m_SyntaxID = UUID.Zero;
        private string m_SyntaxDir = "./LSLSyntax/";

        private string m_Url = "localhost";

        #region IRegionModuleBase implementation

        public void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["LSLSyntax"];
            if (cnf == null)
            {
                enabled = false;
                return;
            }

            if (cnf != null && cnf.GetString("Enabled", "false") != "true")
            {
                enabled = false;
                return;
            }

            string key = cnf.GetString("SyntaxID", "");
            if (!UUID.TryParse(key, out m_SyntaxID))
            {
                m_log.Error("[LSLSyntax] Module was enabled, but no SyntaxID was given, disabling");
            }

            m_SyntaxDir = cnf.GetString("SyntaxDir", m_SyntaxDir);

            m_Url = cnf.GetString("ExternalSyntaxURL", m_Url);

            if(m_Url != "localhost")
            {
                if (!m_Url.EndsWith("/"))
                    m_Url = m_Url + "/";
            }

            m_log.Info("[LSLSyntax] Plugin enabled!");
        }

        public void AddRegion(Scene scene)
        {
            if (!enabled)
                return;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!enabled)
                return;

            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();

            if (featuresModule != null)
                featuresModule.OnSimulatorFeaturesRequest += OnSimulatorFeaturesRequest;

            scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!enabled)
                return;
        }

        public void PostInitialise()
        {
        }

        public string Name
        {
            get { return "LSLSyntaxModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        #endregion

        #region Event Handlers
        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features)
        {
            features["LSLSyntaxId"] = new OSDUUID(m_SyntaxID);
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            if (m_Url == "localhost")
            {
                IRequestHandler LSLSyntaxHandler = new RestStreamHandler(
                    "GET", "/CAPS/" + UUID.Random(), LSLSyntax, "LSLSyntax", null);
                caps.RegisterHandler("LSLSyntax", LSLSyntaxHandler);
            }
            else
            {
                caps.RegisterHandler("LSLSyntax", m_Url + m_SyntaxID);
            }
        }
        #endregion

        #region Cap Handles
        public string LSLSyntax(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse)
        {
            return File.ReadAllText(m_SyntaxDir + m_SyntaxID.ToString() + ".xml");
        }
        #endregion
    }
}

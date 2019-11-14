using log4net;
using Mono.Addins;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
//using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Timers;
using System.Xml;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse.StructuredData;
using OpenSim.Services;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AbuseReports")]
    public class AbuseReportsModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;
        private Scene m_Scene;

        private IAbuseReportsService m_Connector = null;

        private IUserManagement m_UserManager = null;

        #region IRegionModuleBase implementation

        public void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["AbuseReports"];
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

            if (!enabled)
                return;

            m_log.Info("[AbuseReports] Plugin enabled!");
        }

        public void AddRegion(Scene scene)
        {
            if (!enabled)
                return;

            m_Scene = scene;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!enabled)
                return;

            m_UserManager = scene.RequestModuleInterface<IUserManagement>();

            if(m_UserManager == null)
            {
                m_log.Info("[AbuseReports] Plugin disabled because IUserManagement was not found!");
                enabled = false;
                return;
            }

            m_Connector = scene.RequestModuleInterface<IAbuseReportsService>();
            if(m_Connector == null)
            {
                m_log.ErrorFormat("[AbuseReports]: AbuseReportsService not availble in region {0}. Module Disabled", scene.Name);
                enabled = false;
                return;
            }
            
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
            get { return "AbuseReportsModule"; }
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

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            IRequestHandler SendUserReportHandler = new RestStreamHandler(
                "POST", "/CAPS/" + UUID.Random(), (v, w, x, y, z) => SendUserReport(v, w, x, y, z, caps), "SendUserReportHandler", null);
            caps.RegisterHandler("SendUserReport", SendUserReportHandler);

            IRequestHandler SendUserReportWithScreenshotHandler = new RestStreamHandler(
                "POST", "/CAPS/" + UUID.Random(), (v, w, x, y, z) => SendUserReportWithScreenshot(v, w, x, y, z, caps), "SendUserReportWithScreenshot", null);
            caps.RegisterHandler("SendUserReportWithScreenshot", SendUserReportWithScreenshotHandler);
        }

        #endregion

        #region Cap Handles

        private AbuseReportData AbuseReportDataFromOSD(OSDMap map)
        {
            AbuseReportData abuse_report = new AbuseReportData();

            if(map.ContainsKey("abuser-id"))
                abuse_report.AbuserID = map["abuser-id"].AsUUID();
            
            if(map.ContainsKey("category"))
                abuse_report.Category = map["category"].ToString();

            if(map.ContainsKey("check-flags"))
                abuse_report.CheckFlags = map["check-flags"].AsInteger();

            if(map.ContainsKey("details"))
                abuse_report.Details = map["details"].ToString();

            if(map.ContainsKey("object-id"))
                abuse_report.ObjectID = map["object-id"].AsUUID();

            if(map.ContainsKey("position"))
                abuse_report.Position = map["position"].AsVector3().ToString();

            if(map.ContainsKey("report-type"))
                abuse_report.ReportType = map["report-type"].AsInteger();

            if(map.ContainsKey("summary"))
                abuse_report.Summary = map["summary"].ToString();

            if(map.ContainsKey("version-string"))
                abuse_report.Version = map["version-string"].ToString();
            
            return abuse_report;
        }

        public string SendUserReport(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse, Caps caps)
        {
            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.OK;
            httpResponse.ContentType = "text/html";

            OSDMap response = new OSDMap();

            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml(request);

            AbuseReportData abuse_report = AbuseReportDataFromOSD(map);
            abuse_report.SenderID = caps.AgentID;
            abuse_report.SenderName = m_UserManager.GetUserName(caps.AgentID);
            abuse_report.AbuseRegionID = m_Scene.RegionInfo.RegionID;
            abuse_report.AbuseRegionName = m_Scene.RegionInfo.RegionName;
            abuse_report.AbuserName = m_UserManager.GetUserName(abuse_report.AbuserID);
            
            if(m_Connector.ReportAbuse(abuse_report))
            {
                m_log.InfoFormat("[AbuseReports] {0} has reported {1}", abuse_report.SenderName, abuse_report.AbuserName);
                response.Add("state", "complete");
            }
            else
            {
                response.Add("state", "failed");
            }

            return OSDParser.SerializeLLSDXmlString(response); ;
        }

        public string SendUserReportWithScreenshot(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse, Caps caps)
        {
            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.OK;
            httpResponse.ContentType = "text/html";

            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml(request);

            AbuseReportData abuse_report = AbuseReportDataFromOSD(map);
            abuse_report.SenderID = caps.AgentID;
            abuse_report.SenderName = m_UserManager.GetUserName(caps.AgentID);
            abuse_report.AbuseRegionID = m_Scene.RegionInfo.RegionID;
            abuse_report.AbuseRegionName = m_Scene.RegionInfo.RegionName;
            abuse_report.AbuserName = m_UserManager.GetUserName(abuse_report.AbuserID);
            
            UUID screenshot_id = map["screenshot-id"].AsUUID();

            BinaryStreamHandler uploader = new BinaryStreamHandler(
                    "POST", "/CAPS/" + UUID.Random(), (byte[] data, string p, string pa) => {
                        caps.HttpListener.RemoveStreamHandler("POST", p);

                        OSDMap upload_response = new OSDMap();

                        abuse_report.ImageData = data;
                        
						if(m_Connector.ReportAbuse(abuse_report))
                        {
                            m_log.InfoFormat("[AbuseReports] {0} has reported {1}", abuse_report.SenderName, abuse_report.AbuserName);

                            upload_response.Add("state", "complete");
                            upload_response.Add("new_asset", screenshot_id);
                        }
                        else
                        {
                            upload_response.Add("state", "failed");
                        }
                        
                        return OSDParser.SerializeLLSDXmlString(upload_response);
                    }, "", null);
            caps.HttpListener.AddStreamHandler(uploader);

            OSDMap response = new OSDMap();
            response.Add("state", "upload");
            response.Add("uploader", "http://" + caps.HostName + ":" + caps.Port + uploader.Path);

            return OSDParser.SerializeLLSDXmlString(response); ;
        }

        #endregion
    }
}

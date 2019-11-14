using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Framework.ServiceAuth;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class AbuseReportsServicesConnector : BaseServiceConnector, IAbuseReportsService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public AbuseReportsServicesConnector()
        {
        }

        public AbuseReportsServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/') + "/abuse";
        }

        public AbuseReportsServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["AbuseReportsService"];
            if (gridConfig == null)
            {
                m_log.Error("[ABUSE REPORTS CONNECTOR]: AbuseReportsService missing from configuration");
                throw new Exception("AbuseReports connector init error");
            }

            string serviceURI = gridConfig.GetString("AbuseReportsServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[ABUSE REPORTS CONNECTOR]: No Server URI named in section GridUserService");
                throw new Exception("AbuseReports connector init error");
            }
            m_ServerURI = serviceURI + "/abuse";
            base.Initialise(source, "AbuseReportsService");
        }

        #region IAbuseReportsService
        
        public bool ReportAbuse(AbuseReportData report)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "report";
            sendData["reporter"] = report.SenderID.ToString();
            sendData["reporter-name"] = report.SenderName;
            sendData["abuser"] = report.AbuserID.ToString();
            sendData["abuser-name"] = report.AbuserName;
            sendData["summary"] = report.Summary;
            sendData["check-flags"] = report.CheckFlags.ToString();
            sendData["region-id"] = report.AbuseRegionID.ToString();
            sendData["region-name"] = report.AbuseRegionName;
            sendData["category"] = report.Category;
            sendData["version"] = report.Version;
            sendData["details"] = report.Details;
            sendData["object-id"] = report.ObjectID.ToString();
            sendData["position"] = report.Position.ToString();
            sendData["report-type"] = report.ReportType.ToString();
            sendData["image-data"] = Convert.ToBase64String(report.ImageData);

            return doSimplePost(ServerUtils.BuildQueryString(sendData), "report");
         }

        #endregion IAbuseReportsService

        private bool doSimplePost(string reqString, string meth)
        {
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, reqString, m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[ABUSE REPORTS CONNECTOR]: {0} reply data does not contain result field", meth);
                }
                else
                    m_log.DebugFormat("[ABUSE REPORTS CONNECTOR]: {0} received empty reply", meth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ABUSE REPORTS CONNECTOR]: Exception when contacting server at {0}: {1}", m_ServerURI, e.Message);
            }

            return false;
        }
    }
}

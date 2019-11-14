using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.AbuseReports
{
    public class AbuseReportsServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAbuseReportsService m_service;

        public AbuseReportsServerPostHandler(IAbuseReportsService service, IServiceAuth auth) :
                base("POST", "/abuse", auth)
        {
            m_service = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new StreamReader(requestData))
                body = sr.ReadToEnd();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);
            string method = string.Empty;

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                method = request["METHOD"].ToString();

                switch (method)
                {
                    case "report":
                        return report(request);
                }
                m_log.DebugFormat("[ABUSE REPORT HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ABUSE REPORT HANDLER]: Exception in method {0}: {1}", method, e);
            }

            return FailureResult();
        }

        byte[] report(Dictionary<string, object> request)
        {
            if(!request.ContainsKey("reporter") || !request.ContainsKey("abuser"))
                return FailureResult();

            AbuseReportData report = new AbuseReportData();

            if( !UUID.TryParse(request["reporter"].ToString(), out report.SenderID))
                return FailureResult();

            if(request.ContainsKey("reporter-name"))
				report.SenderName = request["reporter-name"].ToString();

            if(!UUID.TryParse(request["abuser"].ToString(), out report.AbuserID))
                return FailureResult();

            if(request.ContainsKey("abuser-name"))
				report.AbuserName = request["abuser-name"].ToString();

            if(!UUID.TryParse(request["region-id"].ToString(), out report.AbuseRegionID))
                return FailureResult();

            if(request.ContainsKey("region-name"))
				report.AbuseRegionName = request["region-name"].ToString();

            report.Time = Util.UnixTimeSinceEpoch();
                
            if(request.ContainsKey("summary"))
				report.Summary = request["summary"].ToString();

            if(request.ContainsKey("details"))
				report.Details = request["details"].ToString();

            if(request.ContainsKey("version"))
				report.Version = request["version"].ToString();

            if(request.ContainsKey("object-id"))
            {
                if(!UUID.TryParse(request["object-id"].ToString(), out report.ObjectID))
                    return FailureResult();
            }
            else
                report.ObjectID = UUID.Zero;

            if(request.ContainsKey("position"))
				report.Position = request["position"].ToString();

            if(request.ContainsKey("category"))
				report.Category = request["category"].ToString();

            if(request.ContainsKey("check-flags"))
            {
                if(!Int32.TryParse(request["check-flags"].ToString(), out report.CheckFlags))
                    return FailureResult();
            }
            else
                report.CheckFlags = 0;

            if(request.ContainsKey("image-data"))
                report.ImageData = Convert.FromBase64String(request["image-data"].ToString());
            else report.ImageData = new byte[0];

            m_log.InfoFormat("[ABUSE REPORTS] {0} has reported {1}", report.SenderName, report.AbuserName);

            return m_service.ReportAbuse(report) ? SuccessResult() : FailureResult();
        }

        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        private byte[] FailureResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }
    }
}

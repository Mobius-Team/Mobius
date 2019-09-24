/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Web;
using System.Xml;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
//using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using Nwc.XmlRpc;

namespace OpenSim.Capabilities.Handlers
{
    public class ExternalAvatarPickerSearchHandler : BaseStreamHandler
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_SearchURL = string.Empty;

        public ExternalAvatarPickerSearchHandler(string path, string search_url, string name, string description)
            : base("GET", path, name, description)
        {
            m_SearchURL = search_url;
        }

        protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            string names = query.GetOne("names");
            string psize = query.GetOne("page_size");
            string pnumber = query.GetOne("page");

            if (string.IsNullOrEmpty(names) || names.Length < 3)
                return FailureResponse(names, (int)System.Net.HttpStatusCode.BadRequest, httpResponse);

            m_log.DebugFormat("[AVATAR PICKER SEARCH]: search for {0}", names);

            int page_size = (string.IsNullOrEmpty(psize) ? 500 : Int32.Parse(psize));
            int page_number = (string.IsNullOrEmpty(pnumber) ? 1 : Int32.Parse(pnumber));

            // Full content request
            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.OK;
            //httpResponse.ContentLength = ??;
            httpResponse.ContentType = "application/llsd+xml";

            Dictionary<UUID, NameInfo> searchResult = SearchDisplayNames(names, page_size, page_number);

            LLSDAvatarPicker osdReply = new LLSDAvatarPicker();
            osdReply.next_page_url = httpRequest.RawUrl;
            foreach (KeyValuePair<UUID, NameInfo> pair in searchResult)
                osdReply.agents.Array.Add(ConvertNameInfo(pair.Key, pair.Value));

            string reply = LLSDHelpers.SerialiseLLSDReply(osdReply);
            return System.Text.Encoding.UTF8.GetBytes(reply);
        }

        private LLSDPerson ConvertNameInfo(UUID agent, NameInfo info)
        {
            LLSDPerson p = new LLSDPerson();
            p.id = agent;
            p.legacy_first_name = info.FirstName;
            p.legacy_last_name = info.LastName;

            if (!info.IsLocal)
            {
                string[] split = info.FirstName.Split('.');
                p.username = ((split[1].ToLower() == "resident" ? split[0].ToLower() : string.Format("{0}.{1}", split[0], split[1])) + info.LastName).ToLower();
            }
            else p.username = info.UserName;
            
            p.display_name = info.DisplayName;
            p.is_display_name_default = info.IsDefault;
            
            return p;
        }

        private byte[] FailureResponse(string names, int statuscode, IOSHttpResponse httpResponse)
        {
            m_log.Error("[AVATAR PICKER SEARCH]: Error searching for " + names);
            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
            return System.Text.Encoding.UTF8.GetBytes(string.Empty);
        }

        public Dictionary<UUID, NameInfo> SearchDisplayNames(string search, int page_size, int page_number)
        {
            Dictionary<UUID, NameInfo> result = new Dictionary<UUID, NameInfo>();
            
            Hashtable ReqHash = new Hashtable();
            ReqHash["SearchValue"] = search;
			ReqHash["PageSize"] = page_size;
			ReqHash["PageNumber"] = page_number;

            Hashtable xmlrpc_result = GenericXMLRPCRequest(ReqHash, "searchavatars", m_SearchURL);

            Hashtable naming = null;

            bool success = false;
            if (xmlrpc_result.ContainsKey("success") && xmlrpc_result["success"] != null)
                success = Convert.ToBoolean(xmlrpc_result["success"]);

            if (success && xmlrpc_result.ContainsKey("names") && xmlrpc_result["names"] != null)
                naming = xmlrpc_result["names"] as Hashtable;
            
            if (success == true && naming != null)
            {
                foreach (DictionaryEntry pair in naming)
                {
                    string userID = pair.Key.ToString();

                    Hashtable name = (Hashtable)pair.Value;
                    
                    if(name.ContainsKey("IsHG")) // just in case...
                    {
                        NameInfo theName = new NameInfo();

                        bool is_hg = Convert.ToBoolean(name["IsHG"].ToString());
                        UUID agentID = UUID.Zero;

                        if(is_hg)
                        {
                            string[] parse = userID.Split(';');
                            if (parse.Length < 3) continue;

                            string nuuid = parse[0];
                            string nhome = parse[1];
                            string nname = parse[2];

                            if (UUID.TryParse(nuuid, out agentID) == false) continue;

                            theName.HomeURI = nhome;
                            theName.FirstName = string.Join(".", nname.Split(' '));
                            theName.LastName = "@" + new Uri(nhome).Authority;

                            if (name["DisplayName"] != null && !string.IsNullOrWhiteSpace("DisplayName"))
                                theName.DisplayName = WebUtility.UrlDecode( name["DisplayName"].ToString() );
                            else
                            {
                                string[] split = nname.Split(' ');
                                theName.DisplayName = split[1].ToLower() == "resident" ? split[0] : nname;
                            }
                        }
                        else
                        {
                            if (UUID.TryParse(userID, out agentID) == false) continue;

                            if (name["FirstName"] == null || name["LastName"] == null) continue;

                            theName.FirstName = name["FirstName"].ToString();
                            theName.LastName = name["LastName"].ToString();

                            if (name["DisplayName"] != null && !string.IsNullOrWhiteSpace(name["DisplayName"].ToString()))
                                theName.DisplayName = WebUtility.UrlDecode( name["DisplayName"].ToString() );
                        }
                        
                        if (agentID.Equals(UUID.Zero) == false)
                            result[agentID] = theName;
                    }
                }
            }

            return result;
        }

        //
        // Make external XMLRPC request
        //
        private Hashtable GenericXMLRPCRequest(Hashtable ReqParams, string method, string server)
        {
            ArrayList SendParams = new ArrayList();
            SendParams.Add(ReqParams);

            // Send Request
            XmlRpcResponse Resp;
            try
            {
                XmlRpcRequest Req = new XmlRpcRequest(method, SendParams);
                Resp = Req.Send(server, 30000);
            }
            catch (WebException ex)
            {
                m_log.ErrorFormat("[DISPLAY NAMES]: Unable to connect to display names " +
                        "server {0}.  Exception {1}", m_SearchURL, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to connect to display names server at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (SocketException ex)
            {
                m_log.ErrorFormat(
                        "[DISPLAY NAMES]: Unable to connect to display names server {0}. Method {1}, params {2}. " +
                        "Exception {3}", m_SearchURL, method, ReqParams, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to connect to display names server at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (XmlException ex)
            {
                m_log.ErrorFormat(
                        "[DISPLAY NAMES]: Unable to connect to display names server {0}. Method {1}, params {2}. " +
                        "Exception {3}", m_SearchURL, method, ReqParams, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to connect to display names server at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            if (Resp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to process display names response at this time. ";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }
            Hashtable RespData = (Hashtable)Resp.Value;

            return RespData;
        }
    }
}
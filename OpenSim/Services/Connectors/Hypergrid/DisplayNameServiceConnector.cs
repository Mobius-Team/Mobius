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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Simulation;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nwc.XmlRpc;
using Nini.Config;
using System.Threading.Tasks;
using OpenSim.Server.Base;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class DisplayNameServiceConnector
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURLHost;
        private string m_ServerURL;

        public DisplayNameServiceConnector(string url) : this(url, true)
        {
        }

        public DisplayNameServiceConnector(string url, bool dnsLookup)
        {
            m_ServerURL = m_ServerURLHost = url;

            if (dnsLookup)
            {
                try
                {
                    Uri m_Uri = new Uri(m_ServerURL);
                    IPAddress ip = Util.GetHostFromDNS(m_Uri.Host);
                    if(ip != null)
                    {
                        m_ServerURL = m_ServerURL.Replace(m_Uri.Host, ip.ToString());
                        if (!m_ServerURL.EndsWith("/"))
                            m_ServerURL += "/";
                    }
                    else
                        m_log.DebugFormat("[DISPLAY NAME CONNECTOR]: Failed to resolve address of {0}", url);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[DISPLAY NAME CONNECTOR]: Malformed Uri {0}: {1}", url, e.Message);
                }
            }
        }

        public Dictionary<UUID, string> GetDisplayNames (UUID[] userIDs)
        {
            string uri = m_ServerURL + "get_display_names";
            
            List<string> str_userIDs = new List<string>();
            foreach(UUID id in userIDs) str_userIDs.Add(id.ToString());

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentIDs"] = new List<string>(str_userIDs);

            string reqString = ServerUtils.BuildQueryString(sendData);

            Dictionary<UUID, string> data = new Dictionary<UUID, string>();
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString, 5, null, false);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if((string)replyData?["success"] == "true")
                    {
                        int i = 0;
                        while(true)
                        {
                            if(replyData.ContainsKey("uuid" + i) && replyData.ContainsKey("name" + i))
                            {
                                string str_uuid = replyData["uuid" + i].ToString();
                                string name = replyData["name" + i].ToString();

                                UUID uuid = UUID.Parse(str_uuid);
                                data.Add(uuid, name);
                                i++;
                            }
                            else break;
                        }
                    }

                }
            }
            catch //(Exception e)
            {
                // target grid is offline or didn't send back the expected result
                //m_log.DebugFormat("[HGGetDisplayNames Connector]: Exception when contacting friends server at {0}: {1}", uri, e.Message);
            }

            return data;
        }

    }
}

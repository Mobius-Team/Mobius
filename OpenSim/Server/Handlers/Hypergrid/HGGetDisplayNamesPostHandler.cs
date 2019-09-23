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
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HGGetDisplayNamesPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAccountService m_UserAccountService;

        public HGGetDisplayNamesPostHandler(IUserAccountService userAccountService) :
                base("POST", "/get_display_names")
        {
            m_UserAccountService = userAccountService;

            if (m_UserAccountService == null)
                m_log.ErrorFormat("[HGGetDisplayNames Handler]: UserAccountService is null!");
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new StreamReader(requestData))
                body = sr.ReadToEnd();
            body = body.Trim();
            
            //m_log.DebugFormat("[get_display_names]: query String: {0}", body);

            Dictionary<string, object> request = ServerUtils.ParseQueryString(body);
            
            
            List<string> userIDs;

            if (!request.ContainsKey("AgentIDs"))
            {
                m_log.DebugFormat("[GRID USER HANDLER]: get_display_names called without required uuids argument");
                return new byte[0];
            }

            if (!(request["AgentIDs"] is List<string>))
            {
                m_log.DebugFormat("[GRID USER HANDLER]: get_display_names input argument was of unexpected type {0}", request["uuids"].GetType().ToString());
                return new byte[0];
            }

            userIDs = (List<string>)request["AgentIDs"];

            List<UserAccount> userAccounts = m_UserAccountService.GetUserAccounts(UUID.Zero, userIDs);

            Dictionary<string, object> result = new Dictionary<string, object>();

            int i = 0;
            foreach(UserAccount user in userAccounts)
            {
                result["uuid" + i] = user.PrincipalID;
                result["name" + i] = user.DisplayName;
                i++;
            }

            result["success"] = "true";
			
            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.InfoFormat("[get_display_name]: response string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }
    }
}

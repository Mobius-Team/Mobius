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
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework.Monitoring;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ViewerAssetModule")]
    public class ViewerAssetModule : INonSharedRegionModule
    {
        class APollRequest
        {
            public PollServiceAssetEventArgs thepoll;
            public UUID reqID;
            public Hashtable request;
            public bool send503;
        }

        public class APollResponse
        {
            public Hashtable response;
            public int bytes;
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        private static ViewerAssetHandler m_ViewerAssetHandler;

        private IAssetService m_assetService = null;

        private Dictionary<UUID, string> m_capsDict = new Dictionary<UUID, string>();
        private static Thread[] m_workerThreads = null;
        private static int m_NumberScenes = 0;
        private static BlockingCollection<APollRequest> m_queue = new BlockingCollection<APollRequest>();

        private Dictionary<UUID, PollServiceAssetEventArgs> m_pollservices = new Dictionary<UUID, PollServiceAssetEventArgs>();

        private string m_GetMeshURL = "localhost";
        private string m_GetMesh2URL = "localhost";
        private string m_GetTextureURL = "localhost";
        private string m_ViewerAssetURL = "localhost";

        private string m_ExternalViewerAssetsURL = "";

        private int m_NumberOfWorkThreads = 2;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];

            if (config == null)
                return;

            m_GetMeshURL = config.GetString("Cap_GetMesh", "localhost");
            m_GetMesh2URL = config.GetString("Cap_GetMesh2", "localhost");
            m_GetTextureURL = config.GetString("Cap_GetTexture", "localhost");
            m_ViewerAssetURL = config.GetString("Cap_ViewerAsset", "localhost");

            m_NumberOfWorkThreads = config.GetInt("AssetWorkThreads", m_NumberOfWorkThreads);

            m_ExternalViewerAssetsURL = config.GetString("ExternalViewerAssetsURL", m_ExternalViewerAssetsURL);
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            s.EventManager.OnRegisterCaps -= RegisterCaps;
            s.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_NumberScenes--;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (m_assetService == null)
            {
                m_assetService = s.RequestModuleInterface<IAssetService>();
                // We'll reuse the same handler for all requests.
                m_ViewerAssetHandler = new ViewerAssetHandler(m_assetService, m_ExternalViewerAssetsURL);
            }

            s.EventManager.OnRegisterCaps += RegisterCaps;
            s.EventManager.OnDeregisterCaps += DeregisterCaps;

            m_NumberScenes++;

            if (m_workerThreads == null)
            {
                m_workerThreads = new Thread[m_NumberOfWorkThreads];

                for (uint i = 0; i < m_NumberOfWorkThreads; i++)
                {
                    m_workerThreads[i] = WorkManager.StartThread(DoAssetRequests,
                            String.Format("ViewerAssetWorker{0}", i),
                            ThreadPriority.Normal,
                            true,
                            false,
                            null,
                            int.MaxValue);
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            if (m_NumberScenes <= 0 && m_workerThreads != null)
            {
                m_log.DebugFormat("[ViewerAssetModule] Closing");

                foreach (Thread t in m_workerThreads)
                    Watchdog.AbortThread(t.ManagedThreadId);

                m_queue.Dispose();
            }
        }

        public string Name { get { return "ViewerAssetModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private class PollServiceAssetEventArgs : PollServiceEventArgs
        {
            private List<Hashtable> requests =
                    new List<Hashtable>();
            private Dictionary<UUID, APollResponse> responses =
                    new Dictionary<UUID, APollResponse>();
            private HashSet<UUID> dropedResponses = new HashSet<UUID>();

            private Scene m_scene;
            private ScenePresence m_presence;
            public PollServiceAssetEventArgs(UUID pId, Scene scene) :
                    base(null, "", null, null, null, null, pId, int.MaxValue)
            {
                m_scene = scene;
                // x is request id, y is userid
                HasEvents = (x, y) =>
                {
                    lock (responses)
                    {
                        APollResponse response;
                        if (responses.TryGetValue(x, out response))
                        {
                            if (m_presence == null)
                                m_presence = m_scene.GetScenePresence(pId);

                            if (m_presence == null || m_presence.IsDeleted)
                                return true;
                            return m_presence.CapCanSendAsset(0, response.bytes);
                        }
                        return false;
                    }
                };

                Drop = (x, y) =>
                {
                    lock (responses)
                    {
                        responses.Remove(x);
                        dropedResponses.Add(x);
                    }
                };

                GetEvents = (x, y) =>
                {
                    lock (responses)
                    {
                        try
                        {
                            return responses[x].response;
                        }
                        finally
                        {
                            responses.Remove(x);
                        }
                    }
                };
                // x is request id, y is request data hashtable
                Request = (x, y) =>
                {
                    APollRequest reqinfo = new APollRequest();
                    reqinfo.thepoll = this;
                    reqinfo.reqID = x;
                    reqinfo.request = y;
                    reqinfo.send503 = false;

                    lock (responses)
                    {
                        if (responses.Count > 0 && m_queue.Count > 32)
                            reqinfo.send503 = true;
                    }

                    m_queue.Add(reqinfo);
                };

                // this should never happen except possible on shutdown
                NoEvents = (x, y) =>
                {
                    Hashtable response = new Hashtable();

                    response["int_response_code"] = 500;
                    response["str_response_string"] = "timeout";
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;
                    return response;
                };
            }

            public void Process(APollRequest requestinfo)
            {
                Hashtable response;

                UUID requestID = requestinfo.reqID;

                if (m_scene.ShuttingDown)
                    return;

                lock (responses)
                {
                    lock (dropedResponses)
                    {
                        if (dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            return;
                        }
                    }

                    if (m_presence == null)
                        m_presence = m_scene.GetScenePresence(Id);

                    if (m_presence == null || m_presence.IsDeleted)
                        requestinfo.send503 = true;

                    if (requestinfo.send503)
                    {
                        response = new Hashtable();

                        response["int_response_code"] = 503;
                        response["str_response_string"] = "Throttled";
                        response["content_type"] = "text/plain";
                        response["keepalive"] = false;

                        Hashtable headers = new Hashtable();
                        headers["Retry-After"] = 20;
                        response["headers"] = headers;

                        responses[requestID] = new APollResponse() { bytes = 0, response = response };
                        return;
                    }
                }

                response = m_ViewerAssetHandler.Handle(requestinfo.request);

                lock (responses)
                {
                    lock (dropedResponses)
                    {
                        if (dropedResponses.Contains(requestID))
                        {
                            dropedResponses.Remove(requestID);
                            return;
                        }
                    }
                    responses[requestID] = new APollResponse()
                    {
                        bytes = (int)response["int_bytes"],
                        response = response
                    };
                }
            }
        }

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            if(m_GetTextureURL == "localhost" || m_GetMeshURL == "localhost" || 
                m_GetMesh2URL == "localhost" || m_ViewerAssetURL == "localhost")
            {
                string capUrl = "/CAPS/" + UUID.Random();

                // Register this as a poll service
                PollServiceAssetEventArgs args = new PollServiceAssetEventArgs(agentID, m_scene);

                args.Type = PollServiceEventArgs.EventType.Texture;
                MainServer.Instance.AddPollServiceHTTPHandler(capUrl, args);

                string hostName = m_scene.RegionInfo.ExternalHostName;
                uint port = (MainServer.Instance == null) ? 0 : MainServer.Instance.Port;
                string protocol = "http";

                if (MainServer.Instance.UseSSL)
                {
                    hostName = MainServer.Instance.SSLCommonName;
                    port = MainServer.Instance.SSLPort;
                    protocol = "https";
                }

                IExternalCapsModule handler = m_scene.RequestModuleInterface<IExternalCapsModule>();

                string cap_url = String.Format("{0}://{1}:{2}{3}", protocol, hostName, port, capUrl);

                if (handler != null)
                {
                    if (m_GetTextureURL == "localhost")
                        handler.RegisterExternalUserCapsHandler(agentID, caps, "GetTexture", capUrl);
                    if (m_GetMeshURL == "localhost")
                        handler.RegisterExternalUserCapsHandler(agentID, caps, "GetMesh", capUrl);
                    if (m_GetMesh2URL == "localhost")
                        handler.RegisterExternalUserCapsHandler(agentID, caps, "GetMesh2", capUrl);
                    if (m_ViewerAssetURL == "localhost")
                        handler.RegisterExternalUserCapsHandler(agentID, caps, "ViewerAsset", capUrl);
                }
                else
                {
                    if (m_GetTextureURL == "localhost")
                        caps.RegisterHandler("GetTexture", cap_url);
                    if (m_GetMeshURL == "localhost")
                        caps.RegisterHandler("GetMesh", cap_url);
                    if (m_GetMesh2URL == "localhost")
                        caps.RegisterHandler("GetMesh2", cap_url);
                    if (m_ViewerAssetURL == "localhost")
                        caps.RegisterHandler("ViewerAsset", cap_url);
                }

                m_pollservices[agentID] = args;
                m_capsDict[agentID] = capUrl;
            }

            if (m_GetTextureURL != "localhost" && !string.IsNullOrWhiteSpace(m_GetTextureURL))
                caps.RegisterHandler("GetTexture", m_GetTextureURL);
            if (m_GetMeshURL != "localhost" && !string.IsNullOrWhiteSpace(m_GetMeshURL))
                caps.RegisterHandler("GetMesh", m_GetMeshURL);
            if (m_GetMesh2URL != "localhost" && !string.IsNullOrWhiteSpace(m_GetMesh2URL))
                caps.RegisterHandler("GetMesh2", m_GetMesh2URL);
            if (m_ViewerAssetURL != "localhost" && !string.IsNullOrWhiteSpace(m_ViewerAssetURL))
                caps.RegisterHandler("ViewerAsset", m_ViewerAssetURL);
        }

        private void DeregisterCaps(UUID agentID, Caps caps)
        {
            PollServiceAssetEventArgs args;

            MainServer.Instance.RemoveHTTPHandler("", m_GetTextureURL);
            MainServer.Instance.RemoveHTTPHandler("", m_GetMeshURL);
            MainServer.Instance.RemoveHTTPHandler("", m_GetMesh2URL);
            MainServer.Instance.RemoveHTTPHandler("", m_ViewerAssetURL);

            m_capsDict.Remove(agentID);

            if (m_pollservices.TryGetValue(agentID, out args))
            {
                m_pollservices.Remove(agentID);
            }
        }

        private static void DoAssetRequests()
        {
            APollRequest poolreq;
            while (m_NumberScenes > 0)
            {
                poolreq = null;
                if (!m_queue.TryTake(out poolreq, 4500) || poolreq == null)
                {
                    Watchdog.UpdateThread();
                    continue;
                }

                if (m_NumberScenes <= 0)
                    break;

                Watchdog.UpdateThread();
                if (poolreq.reqID != UUID.Zero)
                    poolreq.thepoll.Process(poolreq);
            }
        }
    }
}

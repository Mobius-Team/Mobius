/*
 * Copyright (c) FoTo50
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
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Framework.Console;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse.Packets;
using Mono.Addins;


//[assembly: Addin("jOpenSimProfile", "0.1")]
//[assembly: AddinDependency("OpenSim", "0.5")]
[assembly: Addin("jOpenSim.Profile", "0.0.1.0")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDescription("Profile module working with jOpenSim component")]
[assembly: AddinAuthor("FoTo50 modded by BillBlight")]


namespace jOpenSim.Profile.jOpenProfile
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class jOpenSimProfileModule : ISharedRegionModule
    {
        //
        // Log module
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private IConfigSource m_gConfig;
        private List<Scene> m_Scenes = new List<Scene>();
        private string m_ProfileServer = "";
        private string m_ProfileModul = "";
        private Scene m_parentScene;
        private Scene m_scene;
        private bool m_Enabled = false;
        public string m_moduleName = "jOpenSimProfile";
        public string m_moduleVersion = "0.0.1.0";
        public bool m_Debug = false;
        public string compileVersion = OpenSim.VersionInfo.VersionNumber;

        public void Initialise(IConfigSource config)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] Initialise", m_moduleName);
            }
            IConfig profileConfig = config.Configs["Profile"];

            if (m_Scenes.Count == 0) // First time
            {
                if (profileConfig == null)
                {
                    m_log.ErrorFormat("[{0}]: jOpenSimProfile disabled! Reason: [Profile] section not found", m_moduleName);
                    m_Enabled = false;
                    return;
                }
                m_ProfileServer = profileConfig.GetString("ProfileURL", "");
                m_ProfileModul  = profileConfig.GetString("Module", "");
                m_Debug = profileConfig.GetBoolean("Debug", false);

                if (m_ProfileModul != "jOpenSimProfile")
                {
                    m_log.ErrorFormat("[{0}]: disabled! Reason: Module Name in [Profile] config section invalid or not found", m_moduleName);
                    m_Enabled = false;
                    return;
                }

                if (m_ProfileServer == "")
                {
                    m_log.ErrorFormat("[{0}]: disabled (no ProfileURL found)", m_moduleName);
                    m_Enabled = false;
                    return;
                }
                else
                {
                    m_log.InfoFormat("[{0}] activated, communicating with {1}", m_moduleName,m_ProfileServer);
                    m_Enabled = true;
                }
            }
            m_gConfig = config;
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_parentScene = scene;

            if (!m_Scenes.Contains(scene))
                m_Scenes.Add(scene);
            // Hook up events
            scene.EventManager.OnNewClient += OnNewClient;
            scene.RegisterModuleInterface(this);
        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_moduleName+" "+m_moduleVersion; }
        }

        public void RegionLoaded(Scene scene)
        {
            InstallInterfaces();
            scene.RegisterModuleInterface(this);
        }
 
        public void RemoveRegion(Scene scene)
        {
            // do nothing
        }
        
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        ScenePresence FindPresence(UUID clientID)
        {
            ScenePresence p;

            foreach (Scene s in m_Scenes)
            {
                p = s.GetScenePresence(clientID);
                if (p != null && !p.IsChildAgent)
                    return p;
            }
            return null;
        }

        /// New Client Event Handler
        private void OnNewClient(IClientAPI client)
        {
            // Subscribe to messages

            // Classifieds
            client.AddGenericPacketHandler("avatarclassifiedsrequest", HandleAvatarClassifiedsRequest);
            client.OnClassifiedInfoRequest += ClassifiedInfoRequest;
            client.OnClassifiedInfoUpdate += ClassifiedInfoUpdate;
            client.OnClassifiedDelete += ClassifiedDelete;

            // Picks
            client.AddGenericPacketHandler("avatarpicksrequest", HandleAvatarPicksRequest);
            client.AddGenericPacketHandler("pickinforequest", HandlePickInfoRequest);
            client.OnPickInfoUpdate += PickInfoUpdate;
            client.OnPickDelete += PickDelete;

            // Notes
            client.AddGenericPacketHandler("avatarnotesrequest", HandleAvatarNotesRequest);
            client.OnAvatarNotesUpdate += AvatarNotesUpdate;

            //Profile
            client.OnRequestAvatarProperties += RequestAvatarProperties;
            client.OnUpdateAvatarProperties += UpdateAvatarProperties;
            client.OnAvatarInterestUpdate += AvatarInterestsUpdate;
            client.OnUserInfoRequest += UserPreferencesRequest;
            client.OnUpdateUserInfo += UpdateUserPreferences;

            AgentCircuitData clientinfo = client.RequestClientInfo();
            string agentID = client.AgentId.ToString();
            string agentIP = client.RemoteEndPoint.ToString();
            string agentName = clientinfo.Name;
            // Fill parameters for informing jOpenSim about this user.
            Hashtable paramTable = new Hashtable();
            paramTable["agentID"] = agentID;
            paramTable["agentIP"] = agentIP;
            paramTable["agentName"] = agentName;

            // Generate the request for transfer.
            Hashtable resultTable = GenericXMLRPCRequest(paramTable, "clientInfo");
        }


        private void InstallInterfaces()
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand(m_moduleName, false, "profile version", "profile version", "Displaying the version of jOpenSimProfile", displayversion);

            }

            //            Command updateSearchCommand = new Command("search-update", CommandIntentions.COMMAND_NON_HAZARDOUS, jUpdateSearch, "Updates Search Database.");
            //            m_commander.RegisterCommand("search-update", updateSearchCommand);
            // Add this to our scene so scripts can call these functions
            //			m_parentScene.RegisterModuleCommander(m_commander);
        }


        public void displayversion(string module, string[] cmd)
        {
            m_log.Info("");
            m_log.InfoFormat("[{0}] my version is: {1} (compiled with OpenSim {2})", m_moduleName, m_moduleVersion, compileVersion);
            m_log.Info("");
        }

        //
        // Make external XMLRPC request
        //
        private Hashtable GenericXMLRPCRequest(Hashtable ReqParams, string method)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] GenericXMLRPCRequest for method {1}", m_moduleName, method);
            }
            
            ArrayList SendParams = new ArrayList();
            SendParams.Add(ReqParams);

            // Send Request
            XmlRpcResponse Resp;
            try
            {
                XmlRpcRequest Req = new XmlRpcRequest(method, SendParams);
                Resp = Req.Send(m_ProfileServer, 30000);
            }
            catch (WebException ex)
            {
                m_log.ErrorFormat("[{0}]: Unable to connect to Profile Server {1}.  Exception {2}", m_moduleName, m_ProfileServer, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to fetch profile data at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (SocketException ex)
            {
                m_log.ErrorFormat(
                        "[{0}]: Unable to connect to Profile Server {1}. Method {2}, params {3}. Exception {4}", m_moduleName, m_ProfileServer, method, ReqParams, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to fetch profile data at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (XmlException ex)
            {
                m_log.ErrorFormat(
                        "[{0}]: Unable to connect to Profile Server {1}. Method {2}, params {3}. Exception {4}", m_moduleName, m_ProfileServer, method, ReqParams.ToString(), ex);
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to fetch profile data at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            if (Resp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to fetch profile data at this time. ";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }
            Hashtable RespData = (Hashtable)Resp.Value;

            return RespData;
        }

        // Classifieds Handler

        public void HandleAvatarClassifiedsRequest(Object sender, string method, List<String> args)
        {
            IClientAPI remoteClient = (IClientAPI)sender;
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] HandleAvatarClassifiedsRequest for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }

            if (!(sender is IClientAPI))
            {
	            if (m_Debug)
	            {
	                m_log.DebugFormat("[{0}] HandleAvatarClassifiedsRequest (!(sender {1} is IClientAPI {2}))", m_moduleName, sender.ToString(), remoteClient.AgentId.ToString());
	            }
                return;
            }

            Hashtable ReqHash = new Hashtable();
            ReqHash["uuid"] = args[0];

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    method);

            if (!Convert.ToBoolean(result["success"]))
            {
                remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            Dictionary<UUID, string> classifieds = new Dictionary<UUID, string>();

            foreach (Object o in dataArray)
            {
                Hashtable d = (Hashtable)o;

                classifieds[new UUID(d["classifiedid"].ToString())] = d["name"].ToString();
            }

            remoteClient.SendAvatarClassifiedReply(new UUID(args[0]), classifieds);
        }

        // Request Classifieds
        public void ClassifiedInfoRequest(UUID classifiedID, IClientAPI remoteClient)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] ClassifiedInfoRequest for AgentId {1} for {2}", m_moduleName, remoteClient.AgentId.ToString(), classifiedID.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"]      = remoteClient.AgentId.ToString();
            ReqHash["classified_id"] = classifiedID.ToString();
            
            Hashtable result = GenericXMLRPCRequest(ReqHash, "classifiedinforequest");
            if (!Convert.ToBoolean(result["success"]))
            {
                remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            if (dataArray.Count == 0)
            {
                if (m_Debug)
                {
                    m_log.DebugFormat("[{0}] nothing returned at ClassifiedInfoRequest for AgentId {1} for {2}", m_moduleName, remoteClient.AgentId.ToString(), classifiedID.ToString());
                }
                // something bad happened here, if we could return an
                // event after the search,
                // we should be able to find it here
                // TODO do some (more) sensible error-handling here
                return;
            }

            Hashtable d = (Hashtable)dataArray[0];

            Vector3 globalPos = new Vector3();
            Vector3.TryParse(d["posglobal"].ToString(), out globalPos);

            if (d["description"]==null) d["description"] = String.Empty;

            string name = d["name"].ToString();
            string desc = d["description"].ToString();

            remoteClient.SendClassifiedInfoReply(new UUID(d["classifieduuid"].ToString()),
                                                 new UUID(d["creatoruuid"].ToString()),
                                                 Convert.ToUInt32(d["creationdate"]),
                                                 Convert.ToUInt32(d["expirationdate"]),
                                                 Convert.ToUInt32(d["category"]),
                                                 name,
                                                 desc,
                                                 new UUID(d["parceluuid"].ToString()),
                                                 Convert.ToUInt32(d["parentestate"]),
                                                 new UUID(d["snapshotuuid"].ToString()),
                                                 d["simname"].ToString(),
                                                 globalPos,
                                                 d["parcelname"].ToString(),
                                                 Convert.ToByte(d["classifiedflags"]),
                                                 Convert.ToInt32(d["priceforlisting"]));
        }

        // Classifieds Update
        public void ClassifiedInfoUpdate(UUID queryclassifiedID, uint queryCategory, string queryName, string queryDescription, UUID queryParcelID,
                                        uint queryParentEstate, UUID querySnapshotID, Vector3 queryGlobalPos, byte queryclassifiedFlags,
                                        int queryclassifiedPrice, IClientAPI remoteClient)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] ClassifiedInfoUpdate for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }

            Hashtable ReqHash = new Hashtable();
            ReqHash["creatorUUID"] = remoteClient.AgentId.ToString();
            ReqHash["classifiedUUID"] = queryclassifiedID.ToString();
            ReqHash["category"] = queryCategory.ToString();
            ReqHash["name"] = queryName;
            ReqHash["description"] = queryDescription;
            ReqHash["parentestate"] = queryParentEstate.ToString();
            ReqHash["snapshotUUID"] = querySnapshotID.ToString();
            ReqHash["sim_name"] = remoteClient.Scene.RegionInfo.RegionName;
            ReqHash["globalpos"] = queryGlobalPos.ToString();
            ReqHash["classifiedFlags"] = queryclassifiedFlags.ToString();
            ReqHash["classifiedPrice"] = queryclassifiedPrice.ToString();

            ScenePresence p = FindPresence(remoteClient.AgentId);

            Vector3 avaPos = p.AbsolutePosition;

            // Getting the parceluuid for this parcel

            ReqHash["parcel_uuid"] = p.currentParcelUUID.ToString();

            // Getting the global position for the Avatar

            Vector3 posGlobal = new Vector3(remoteClient.Scene.RegionInfo.RegionLocX * Constants.RegionSize + avaPos.X,
                                            remoteClient.Scene.RegionInfo.RegionLocY * Constants.RegionSize + avaPos.Y,
                                            avaPos.Z);

            ReqHash["pos_global"] = posGlobal.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "classified_update");

            if (!Convert.ToBoolean(result["success"]))
            {
                remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        // Classifieds Delete

        public void ClassifiedDelete (UUID queryClassifiedID, IClientAPI remoteClient)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] ClassifiedDelete for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["classifiedID"] = queryClassifiedID.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "classified_delete");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        // Picks Handler

        public void HandleAvatarPicksRequest(Object sender, string method, List<String> args)
        {
            IClientAPI remoteClient = (IClientAPI)sender;
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] HandleAvatarPicksRequest for {1} with method {2}", m_moduleName, remoteClient.AgentId.ToString(), method);
            }

            if (!(sender is IClientAPI))
            {
	            if (m_Debug)
	            {
	                m_log.DebugFormat("[{0}] HandleAvatarPicksRequest for (!(sender {1} is IClientAPI {2}))", m_moduleName, sender.ToString(), remoteClient.AgentId.ToString());
	            }
                return;
            }

            Hashtable ReqHash = new Hashtable();
            ReqHash["uuid"] = args[0];

            Hashtable result = GenericXMLRPCRequest(ReqHash,method);

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            Dictionary<UUID, string> picks = new Dictionary<UUID, string>();

            if (dataArray != null)
            {
                foreach (Object o in dataArray)
                {
                    Hashtable d = (Hashtable)o;

                    picks[new UUID(d["pickid"].ToString())] = d["name"].ToString();
                }
            }

            remoteClient.SendAvatarPicksReply(new UUID(args[0]), picks);
        }

        // Picks Request

        public void HandlePickInfoRequest(Object sender, string method, List<String> args)
        {
            IClientAPI remoteClient = (IClientAPI)sender;
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] HandlePickInfoRequest for {1} with method {2}", m_moduleName, remoteClient.AgentId.ToString(), method);
            }

            if (!(sender is IClientAPI))
            {
	            if (m_Debug)
	            {
	                m_log.DebugFormat("[{0}] HandlePickInfoRequest for (!(sender {1} is IClientAPI {2}))", m_moduleName, sender.ToString(), remoteClient.AgentId.ToString());
	            }
                return;
            }

            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = args[0];
            ReqHash["pick_id"] = args[1];

            Hashtable result = GenericXMLRPCRequest(ReqHash,method);

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            Hashtable d = (Hashtable)dataArray[0];

            Vector3 globalPos = new Vector3();
            Vector3.TryParse(d["posglobal"].ToString(), out globalPos);

            if (d["description"] == null)
                d["description"] = String.Empty;

            remoteClient.SendPickInfoReply(
                    new UUID(d["pickuuid"].ToString()),
                    new UUID(d["creatoruuid"].ToString()),
                    Convert.ToBoolean(d["toppick"]),
                    new UUID(d["parceluuid"].ToString()),
                    d["name"].ToString(),
                    d["description"].ToString(),
                    new UUID(d["snapshotuuid"].ToString()),
                    d["user"].ToString(),
                    d["originalname"].ToString(),
                    d["simname"].ToString(),
                    globalPos,
                    Convert.ToInt32(d["sortorder"]),
                    Convert.ToBoolean(d["enabled"]));
        }

        // Picks Update

        public void PickInfoUpdate(IClientAPI remoteClient, UUID pickID, UUID creatorID, bool topPick, string name, string desc, UUID snapshotID, int sortOrder, bool enabled)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] PickInfoUpdate for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["agent_id"] = remoteClient.AgentId.ToString();
            ReqHash["pick_id"] = pickID.ToString();
            ReqHash["creator_id"] = creatorID.ToString();
            ReqHash["top_pick"] = topPick.ToString();
            ReqHash["name"] = name;
            ReqHash["desc"] = desc;
            ReqHash["snapshot_id"] = snapshotID.ToString();
            ReqHash["sort_order"] = sortOrder.ToString();
            ReqHash["enabled"] = enabled.ToString();
            ReqHash["sim_name"] = remoteClient.Scene.RegionInfo.RegionName;

            ScenePresence p = FindPresence(remoteClient.AgentId);

            Vector3 avaPos = p.AbsolutePosition;

            // Getting the parceluuid for this parcel

            ReqHash["parcel_uuid"] = p.currentParcelUUID.ToString();

            // Getting the global position for the Avatar

            Vector3 posGlobal = new Vector3(remoteClient.Scene.RegionInfo.RegionLocX*Constants.RegionSize + avaPos.X,
                                            remoteClient.Scene.RegionInfo.RegionLocY*Constants.RegionSize + avaPos.Y,
                                            avaPos.Z);

            ReqHash["pos_global"] = posGlobal.ToString();

            // Getting the owner of the parcel
            ReqHash["user"] = "";   //FIXME: Get avatar/group who owns parcel

            // Do the request
            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "picks_update");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        // Picks Delete

        public void PickDelete(IClientAPI remoteClient, UUID queryPickID)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] PickDelete for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["pick_id"] = queryPickID.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,"picks_delete");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        // Notes Handler

        public void HandleAvatarNotesRequest(Object sender, string method, List<String> args)
        {
            IClientAPI remoteClient = (IClientAPI)sender;
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] HandleAvatarNotesRequest for {1} with method {2}", m_moduleName, remoteClient.AgentId.ToString(),method);
            }
            string targetid;
            string notes = "";

            if (!(sender is IClientAPI))
            {
	            if (m_Debug)
	            {
	                m_log.DebugFormat("[{0}] HandleAvatarNotesRequest for (!(sender {1} is IClientAPI {2}))", m_moduleName, sender.ToString(), remoteClient.AgentId.ToString());
	            }
                return;
            }


            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
            ReqHash["uuid"] = args[0];

            Hashtable result = GenericXMLRPCRequest(ReqHash,method);

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            if (dataArray != null && dataArray[0] != null)
            {
                Hashtable d = (Hashtable)dataArray[0];

                targetid = d["targetid"].ToString();
                if (d["notes"] != null) notes = d["notes"].ToString();

                remoteClient.SendAvatarNotesReply(new UUID(targetid), notes);
            }
        }

        // Notes Update

        public void AvatarNotesUpdate(IClientAPI remoteClient, UUID queryTargetID, string queryNotes)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] AvatarNotesUpdate for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
            ReqHash["target_id"] = queryTargetID.ToString();
            ReqHash["notes"] = queryNotes;

            Hashtable result = GenericXMLRPCRequest(ReqHash,"avatar_notes_update");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        // Standard Profile bits
        public void AvatarInterestsUpdate(IClientAPI remoteClient, uint wantmask, string wanttext, uint skillsmask, string skillstext, string languages)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] AvatarInterestsUpdate for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
            ReqHash["wantmask"] = wantmask.ToString();
            ReqHash["wanttext"] = wanttext;
            ReqHash["skillsmask"] = skillsmask.ToString();
            ReqHash["skillstext"] = skillstext;
            ReqHash["languages"] = languages;

            Hashtable result = GenericXMLRPCRequest(ReqHash, "avatar_interests_update");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        public void UserPreferencesRequest(IClientAPI remoteClient)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] UserPreferencesRequest for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = remoteClient.AgentId.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,"user_preferences_request");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                return;
            }

            ArrayList dataArray = (ArrayList)result["data"];

            if (dataArray != null && dataArray[0] != null)
            {
                Hashtable d = (Hashtable)dataArray[0];
                string mail = "";

                if (d["email"] != null) mail = d["email"].ToString();

                remoteClient.SendUserInfoReply(
                        Convert.ToBoolean(d["imviaemail"]),
                        Convert.ToBoolean(d["visible"]),
                        mail);
            }
        }

        public void UpdateUserPreferences(bool imViaEmail, bool visible, IClientAPI remoteClient)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] UpdateUserPreferences for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
            ReqHash["imViaEmail"] = imViaEmail.ToString();
            ReqHash["visible"] = visible.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,"user_preferences_update");

            if (!Convert.ToBoolean(result["success"]))
            {
                //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
            }
        }

        // Profile data like the WebURL
        private Hashtable GetProfileData(UUID userID)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] GetProfileData for {1}", m_moduleName, userID.ToString());
            }
            Hashtable ReqHash = new Hashtable();

            ReqHash["avatar_id"] = userID.ToString();

            Hashtable result = GenericXMLRPCRequest(ReqHash,"avatar_properties_request");

            ArrayList dataArray = (ArrayList)result["data"];

            if (dataArray != null && dataArray[0] != null)
            {
                Hashtable d = (Hashtable)dataArray[0];
                return d;
            }
            return result;
        }

        public void RequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] RequestAvatarProperties for {1}",m_moduleName,avatarID.ToString());
            }

            IScene s = remoteClient.Scene;
            if (!(s is Scene)) return;

            Scene scene = (Scene)s;

            UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, avatarID);
            if (null != account)
            {
                Byte[] charterMember;
                if (account.UserTitle == "")
                {
                    charterMember = new Byte[1];
                    charterMember[0] = (Byte)((account.UserFlags & 0xf00) >> 8);
                }
                else
                {
                    charterMember = Utils.StringToBytes(account.UserTitle);
                }

                Hashtable profileData = GetProfileData(avatarID);
                string profileUrl = String.Empty;
                string aboutText = String.Empty;
                string firstLifeAboutText = String.Empty;
                UUID image = UUID.Zero;
                UUID firstLifeImage = UUID.Zero;
                UUID partner = UUID.Zero;
                uint   wantMask = 0;
                string wantText = String.Empty;
                uint   skillsMask = 0;
                string skillsText = String.Empty;
                string languages = String.Empty;

                if (profileData["ProfileUrl"] != null)
                    profileUrl = profileData["ProfileUrl"].ToString();
                if (profileData["AboutText"] != null)
                    aboutText = profileData["AboutText"].ToString();
                if (profileData["FirstLifeAboutText"] != null)
                    firstLifeAboutText = profileData["FirstLifeAboutText"].ToString();
                if (profileData["Image"] != null)
                    image = new UUID(profileData["Image"].ToString());
                if (profileData["FirstLifeImage"] != null)
                    firstLifeImage = new UUID(profileData["FirstLifeImage"].ToString());
                if (profileData["Partner"] != null)
                    partner = new UUID(profileData["Partner"].ToString());

                if(m_Debug)
                {
                    m_log.DebugFormat("[{0}] received Data:", m_moduleName);
                    m_log.DebugFormat("[{0}] [avatarID]: {1}", m_moduleName,avatarID);
                    m_log.DebugFormat("[{0}] profileData[AboutText]: {1}", m_moduleName,aboutText);
                    m_log.DebugFormat("[{0}] [Created]: {1}", m_moduleName,Util.ToDateTime(account.Created).ToString("M/d/yyyy"));
                    m_log.DebugFormat("[{0}] [charterMember]: {1}", m_moduleName, charterMember);
                    m_log.DebugFormat("[{0}] profileData[firstLifeAboutText]: {1}", m_moduleName, firstLifeAboutText);
                    m_log.DebugFormat("[{0}] [UserFlags]: {1}", m_moduleName, (uint)(account.UserFlags & 0xff));
                    m_log.DebugFormat("[{0}] profileData[firstLifeImage]: {1}", m_moduleName, firstLifeImage);
                    m_log.DebugFormat("[{0}] profileData[image]: {1}", m_moduleName, image);
                    m_log.DebugFormat("[{0}] profileData[profileUrl]: {1}", m_moduleName, profileUrl);
                    m_log.DebugFormat("[{0}] profileData[partner]: {1}", m_moduleName, partner);
                }


                // The PROFILE information is no longer stored in the user
                // account. It now needs to be taken from the XMLRPC
                //
                remoteClient.SendAvatarProperties(avatarID, aboutText,
                          Util.ToDateTime(account.Created).ToString(
                                  "M/d/yyyy"),
                          charterMember, firstLifeAboutText,
                          (uint)(account.UserFlags & 0xff),
                          firstLifeImage, image, profileUrl, partner);

                //Viewer expects interest data when it asks for properties.
                if (profileData["wantmask"] != null)
                    wantMask = Convert.ToUInt32(profileData["wantmask"].ToString());
                if (profileData["wanttext"] != null)
                    wantText = profileData["wanttext"].ToString();

                if (profileData["skillsmask"] != null)
                    skillsMask = Convert.ToUInt32(profileData["skillsmask"].ToString());
                if (profileData["skillstext"] != null)
                    skillsText = profileData["skillstext"].ToString();

                if (profileData["languages"] != null)
                    languages = profileData["languages"].ToString();

                remoteClient.SendAvatarInterestsReply(avatarID, wantMask, wantText,
                                                      skillsMask, skillsText, languages);
            }
            else
            {
                m_log.ErrorFormat("[{0}]: Got null for profile for {1}", m_moduleName, avatarID.ToString());
            }
        }

        public void UpdateAvatarProperties(IClientAPI remoteClient, UserProfileData newProfile)
        {
            if (m_Debug)
            {
                m_log.DebugFormat("[{0}] UpdateAvatarProperties for {1}", m_moduleName, remoteClient.AgentId.ToString());
            }
            // if it's the profile of the user requesting the update, then we change only a few things.
            if (remoteClient.AgentId == newProfile.ID)
            {
                Hashtable ReqHash = new Hashtable();

                ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
                ReqHash["ProfileUrl"] = newProfile.ProfileUrl;
                ReqHash["Image"] = newProfile.Image.ToString();
                ReqHash["AboutText"] = newProfile.AboutText;
                ReqHash["FirstLifeImage"] = newProfile.FirstLifeImage.ToString();
                ReqHash["FirstLifeAboutText"] = newProfile.FirstLifeAboutText;
                ReqHash["userFlags"] = newProfile.UserFlags.ToString();

                Hashtable result = GenericXMLRPCRequest(ReqHash,"avatar_properties_update");

                if (!Convert.ToBoolean(result["success"]))
                {
                    //remoteClient.SendAgentAlertMessage(result["errorMessage"].ToString(), false);
                }

                RequestAvatarProperties(remoteClient, newProfile.ID);
            }
        }
    }
}

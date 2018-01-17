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

// DirFindQuery queryFlags:
//
// All Classic:	285212697	10001000000000000000000011001
// Classifieds:	
// Events:		
// Showcase:	
// Land Sales:	
// Places:		
// People:		1			00000000000000000000000000001
// Groups:		285212688	10001000000000000000000010000 (pg only)
//				301989904	10010000000000000000000010000 (mature only)
//				335544336	10100000000000000000000010000 (adult only)
//				385875984	10111000000000000000000010000 (pg, mature, adult)

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
using OpenSim.Services.Connectors;
using OpenSim.Framework.Console;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse.Packets;
using Mono.Addins;
using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;

//[assembly: Addin("jOpenSimSearch", "0.2")]
//[assembly: AddinDependency("OpenSim", "0.5")]
[assembly: Addin("jOpenSim.Search", "0.4.0.1")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDescription("search module working with jOpenSim component")]
[assembly: AddinAuthor("BillBlight")]

namespace jOpenSim.Search
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "jOpenSimSearch")]
    public class jOpenSimSearch : ISharedRegionModule
	{
		//
		// Log module
		//
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		//
		// Module vars
		//
        private IConfigSource m_config;
		private List<Scene> m_Scenes = new List<Scene>();
		private Scene m_parentScene;
		private Scene m_scene;
		private string m_SearchServer = "";
		private int m_DataUpdate = -1;
		private string m_DataUpdateString = "";
		private bool m_Enabled = true;
        private bool m_debug = false;
        private bool m_searchPeople = true;
        public	string m_moduleName = "jOpenSimSearch";
        public string m_moduleVersion = "0.4.0.1";
		private readonly Commander m_commander = new Commander("jopensim");
		public  string customTimeZone = "";
		private bool forceUpdate = false;
		private Dictionary<ulong, Scene> m_sceneList = new Dictionary<ulong, Scene>();
        public string compileVersion = OpenSim.VersionInfo.VersionNumber;

		#region ICommandableModule Members

		public ICommander CommandInterface
		{
			get { return m_commander; }
		}

		#endregion

	   public void Initialise(IConfigSource source)
		{
            // Handle the parameters errors.
            if (source == null) return;
            try
            {
                m_config = source;
                IConfig searchConfig = m_config.Configs["Search"];

                if (searchConfig == null)
                {
                    m_log.InfoFormat("[{0}] Not configured, disabling", m_moduleName);
                    m_Enabled = false;
                    return;
                }
                
                m_SearchServer = searchConfig.GetString("SearchURL", "");
                m_debug = searchConfig.GetBoolean("DebugMode", false);
                m_searchPeople = searchConfig.GetBoolean("searchPeople", true);
                if (m_SearchServer == "")
				{
                    m_log.ErrorFormat("[{0}] No search server, disabling search", m_moduleName);
					m_Enabled = false;
					return;
				}
                else
                {
                    m_log.InfoFormat("[{0}] Search module is activated", m_moduleName);
                    m_Enabled = true;
                }

                IConfig dataConfig = m_config.Configs["DataSnapshot"];

                if (dataConfig == null)
                {
                    m_log.InfoFormat("[{0}] DataSnapshot not configured, disabling", m_moduleName);
                    m_Enabled = false;
                    return;
                }

                m_DataUpdate = dataConfig.GetInt("default_snapshot_period", -1);
                m_DataUpdateString = dataConfig.GetString("default_snapshot_period", "");

                if (m_DataUpdateString == "")
                {
                    m_log.ErrorFormat("[{0}] DataUpdate disabled - Search will mostly be outdated", m_moduleName);
                }
                else
                {
                    m_log.InfoFormat("[{0}] DataUpdateInterval: {1}", m_moduleName, m_DataUpdateString);
                    m_Enabled = true;
                }
            }

            catch (Exception ex)
            {
                m_log.ErrorFormat("[{0}]: Failed to read configuration file: {1}", m_moduleName, ex);
            }
		}

		public void PostInitialise()
		{
			if (!m_Enabled)
			{
				return;
			}
			InstallInterfaces();
			if(m_parentScene != null) initDataUpdate(m_parentScene, m_DataUpdate);
		}

		public void Close()
		{
		}

        public void AddRegion(Scene scene)
        {
            if (m_debug)
            {
                m_log.DebugFormat("[{0}]: ##### AddRegion #####", m_moduleName);
            }
            scene.RegisterModuleInterface(this);
            m_scene = scene;
            m_parentScene = scene;

            lock (m_sceneList)
            {
                if (m_sceneList.Count == 0)
                {
                }
            }
            if (m_sceneList.ContainsKey(scene.RegionInfo.RegionHandle))
            {
                m_sceneList[scene.RegionInfo.RegionHandle] = scene;
            }
            else
            {
                m_sceneList.Add(scene.RegionInfo.RegionHandle, scene);
            }
            if (!m_Scenes.Contains(scene))
                m_Scenes.Add(scene);
            

//            scene.AddCommand(this, "searchupdate", "Updates jOpenSim Search Database", "Updates jOpenSim Search Database", updateSearch);
/*            if (MainConsole.Instance != null)
            {
//                m_log.ErrorFormat("[{0}] YES -> MainConsole.Instance.Commands.AddCommand", m_moduleName);
                MainConsole.Instance.Commands.AddCommand("jOpenSimSearchModule", false, "search update", "search update",
                "Updates the Search Database", updateSearch);

            }
            else
            {
                m_log.ErrorFormat("[{0}] could not add command for console", m_moduleName);
            }
*/
            // Hook up events
            lock (m_scene)
            {
//            	m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
                m_scene.EventManager.OnNewClient += OnNewClient;
                //m_scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
                m_scene.RegisterModuleInterface(this);
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }
		public string Name
		{
            get { return m_moduleName+" "+m_moduleVersion; }
		}
        public Type ReplaceableInterface
        {
            get { return null; }
        }
		public bool IsSharedModule
		{
			get { return true; }
		}

		/// New Client Event Handler
		private void OnNewClient(IClientAPI client)
		{
			// Subscribe to messages
			client.OnDirPlacesQuery += DirPlacesQuery;
			client.OnDirFindQuery += DirFindQuery;
			client.OnDirPopularQuery += DirPopularQuery;
			client.OnDirLandQuery += DirLandQuery;
			client.OnDirClassifiedQuery += DirClassifiedQuery;
			// Response after Directory Queries
			client.OnEventInfoRequest += EventInfoRequest;
			client.OnClassifiedInfoRequest += ClassifiedInfoRequest;
			client.OnMapItemRequest += HandleMapItemRequest;
		}

		private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
		{
			DataUpdate(m_parentScene);
		}

		public void RegionLoaded(Scene scene)
		{
			//Do this here to give file loaders time to initialize and
			//register their supported file extensions and file formats.
			InstallInterfaces();
         scene.RegisterModuleInterface(this);
		}

/*
 private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "jopensim")
            {
                if (args.Length == 1)
                {
                    m_commander.ProcessConsoleCommand("help", new string[0]);
                    return;
                }

                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }
*/
		private void InstallInterfaces()
		{
			if (MainConsole.Instance != null)
			{
                MainConsole.Instance.Commands.AddCommand(m_moduleName, false, "search update", "search update", "Updates the Search Database", updateSearch);
                MainConsole.Instance.Commands.AddCommand(m_moduleName, false, "search version", "search version", "Displaying the version of jOpenSimSearch", displayversion);

			}

//            Command updateSearchCommand = new Command("search-update", CommandIntentions.COMMAND_NON_HAZARDOUS, jUpdateSearch, "Updates Search Database.");
//            m_commander.RegisterCommand("search-update", updateSearchCommand);
			// Add this to our scene so scripts can call these functions
//			m_parentScene.RegisterModuleCommander(m_commander);
		}

        public Scene GetRandomScene()
        {
            lock (m_sceneList)
            {
                foreach (Scene rs in m_sceneList.Values)
                    return rs;
            }
            return null;
        }

		public void updateSearch(string module, string[] cmd)
		{
        	forceUpdate = true;
			DataUpdate(m_parentScene);
		}

        private void jUpdateSearch(Object[] args)
        {
        	forceUpdate = true;
            DataUpdate(m_parentScene);
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
			if (m_debug)
			{
				m_log.DebugFormat("[{0}]: GenericXMLRPCRequest Method:{1}", m_moduleName, method);
			}
			ArrayList SendParams = new ArrayList();
			SendParams.Add(ReqParams);

			// Send Request
			XmlRpcResponse Resp;

			try
			{
				XmlRpcRequest Req = new XmlRpcRequest(method, SendParams);
				Resp = Req.Send(m_SearchServer, 30000);
                if (m_debug)
                {
                    string reqDebug = Resp.ToString();
                    m_log.DebugFormat("[{0}]: Answer from SearchServer: {1}", m_moduleName, reqDebug);
                }
			}
			catch (WebException ex)
			{
				m_log.ErrorFormat("[{0}]: (1) Unable to connect to Search Server {1}.  Exception {2}", m_moduleName, m_SearchServer, ex);

				Hashtable ErrorHash = new Hashtable();
				ErrorHash["success"] = false;
				ErrorHash["errorMessage"] = "Unable to search at this time. ";
				ErrorHash["errorURI"] = "";

				return ErrorHash;
			}
			catch (SocketException ex)
			{
                m_log.ErrorFormat("[{0}]: (2) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex);

				Hashtable ErrorHash = new Hashtable();
				ErrorHash["success"] = false;
				ErrorHash["errorMessage"] = "Unable to search at this time. ";
				ErrorHash["errorURI"] = "";

				return ErrorHash;
			}
			catch (XmlException ex)
			{
                m_log.ErrorFormat("[{0}]: (3GetHashCode) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.GetHashCode());
                m_log.ErrorFormat("[{0}]: (3data) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.GetType());
                m_log.ErrorFormat("[{0}]: (3HelpLink) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.HelpLink);
                m_log.ErrorFormat("[{0}]: (3InnerException) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.InnerException);
                m_log.ErrorFormat("[{0}]: (3LineNumber) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.LineNumber);
                m_log.ErrorFormat("[{0}]: (3LinePosition) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.LinePosition);
                m_log.ErrorFormat("[{0}]: (3Source) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.Source);
                m_log.ErrorFormat("[{0}]: (3SourceUri) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.SourceUri);
                m_log.ErrorFormat("[{0}]: (3StackTrace) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.StackTrace);
                m_log.ErrorFormat("[{0}]: (3TargetSite) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.TargetSite);
                m_log.ErrorFormat("[{0}]: (3ToString) Unable to connect to Search Server {1}. Exception {2}", m_moduleName, m_SearchServer, ex.ToString());
                Hashtable ErrorHash = new Hashtable();
				ErrorHash["success"] = false;
				ErrorHash["errorMessage"] = "Unable to search at this time. ";
				ErrorHash["errorURI"] = "";

				return ErrorHash;
			}
			if (Resp.IsFault)
			{
				m_log.ErrorFormat("[{0}]: response (IsFault): {1}",m_moduleName, Resp.ToString());
				Hashtable ErrorHash = new Hashtable();
				ErrorHash["success"] = false;
				ErrorHash["errorMessage"] = "Unable to search at this time. ";
				ErrorHash["errorURI"] = "";
				return ErrorHash;
			}
			Hashtable RespData = (Hashtable)Resp.Value;

			return RespData;
		}

		protected void DirPlacesQuery(IClientAPI remoteClient, UUID queryID,
				string queryText, int queryFlags, int category, string simName,
				int queryStart)
		{
			Hashtable ReqHash = new Hashtable();
			ReqHash["text"] = queryText;
			ReqHash["flags"] = queryFlags.ToString();
			ReqHash["category"] = category.ToString();
			ReqHash["sim_name"] = simName;
			ReqHash["query_start"] = queryStart.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"dir_places_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			}

			ArrayList dataArray = (ArrayList)result["data"];

			int count = dataArray.Count;
			if (count > 100)
				count = 101;

			DirPlacesReplyData[] data = new DirPlacesReplyData[count];

			int i = 0;

			foreach (Object o in dataArray)
			{
				Hashtable d = (Hashtable)o;

				data[i] = new DirPlacesReplyData();
				data[i].parcelID = new UUID(d["parcel_id"].ToString());
				data[i].name = d["name"].ToString();
				data[i].forSale = Convert.ToBoolean(d["for_sale"]);
				data[i].auction = Convert.ToBoolean(d["auction"]);
				data[i].dwell = Convert.ToSingle(d["dwell"]);
				i++;
				if (i >= count)
					break;
			}

			remoteClient.SendDirPlacesReply(queryID, data);
		}

		public void DirPopularQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags)
		{
			Hashtable ReqHash = new Hashtable();
			ReqHash["flags"] = queryFlags.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"dir_popular_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			}

			ArrayList dataArray = (ArrayList)result["data"];

			int count = dataArray.Count;
			if (count > 100)
				count = 101;

			DirPopularReplyData[] data = new DirPopularReplyData[count];

			int i = 0;

			foreach (Object o in dataArray)
			{
				Hashtable d = (Hashtable)o;

				data[i] = new DirPopularReplyData();
				data[i].parcelID = new UUID(d["parcel_id"].ToString());
				data[i].name = d["name"].ToString();
				data[i].dwell = Convert.ToSingle(d["dwell"]);
				i++;
				if (i >= count)
					break;
			}

			remoteClient.SendDirPopularReply(queryID, data);
		}

		public void DirLandQuery(IClientAPI remoteClient, UUID queryID,
				uint queryFlags, uint searchType, int price, int area,
				int queryStart)
		{
			Hashtable ReqHash = new Hashtable();
			ReqHash["flags"] = queryFlags.ToString();
			ReqHash["type"] = searchType.ToString();
			ReqHash["price"] = price.ToString();
			ReqHash["area"] = area.ToString();
			ReqHash["query_start"] = queryStart.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"dir_land_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			}

			ArrayList dataArray = (ArrayList)result["data"];

			int count = dataArray.Count;
			if (count > 100)
				count = 101;

			DirLandReplyData[] data = new DirLandReplyData[count];

			int i = 0;

			foreach (Object o in dataArray)
			{
				Hashtable d = (Hashtable)o;

				if (d["name"] == null)
					continue;

				data[i] = new DirLandReplyData();
				data[i].parcelID = new UUID(d["parcel_id"].ToString());
				data[i].name = d["name"].ToString();
				data[i].auction = Convert.ToBoolean(d["auction"]);
				data[i].forSale = Convert.ToBoolean(d["for_sale"]);
				data[i].salePrice = Convert.ToInt32(d["sale_price"]);
				data[i].actualArea = Convert.ToInt32(d["area"]);
				i++;
				if (i >= count)
					break;
			}

			remoteClient.SendDirLandReply(queryID, data);
		}

		public void DirFindQuery(IClientAPI remoteClient, UUID queryID,
				string queryText, uint queryFlags, int queryStart)
		{
			if (m_debug)
			{
                m_log.DebugFormat("[{0}]: DirFindQuery queryText:{1}, queryFlags:{2}", m_moduleName, queryText, queryFlags.ToString());
			}
			if (((queryFlags & 1) != 0) && m_searchPeople)
			{
				DirPeopleQuery(remoteClient, queryID, queryText, queryFlags,
						queryStart);
				return;
			}
			else if ((queryFlags & 32) != 0)
			{
				DirEventsQuery(remoteClient, queryID, queryText, queryFlags,
						queryStart);
				return;
			}
		}

		public void DirPeopleQuery(IClientAPI remoteClient, UUID queryID,
				string queryText, uint queryFlags, int queryStart)
		{
			List<UserAccount> accounts = m_Scenes[0].UserAccountService.GetUserAccounts(m_Scenes[0].RegionInfo.ScopeID, queryText);

			DirPeopleReplyData[] data =
					new DirPeopleReplyData[accounts.Count];

			int i = 0;
			foreach (UserAccount item in accounts)
			{
				data[i] = new DirPeopleReplyData();

				data[i].agentID = item.PrincipalID;
				data[i].firstName = item.FirstName;
				data[i].lastName = item.LastName;
				data[i].group = "";
				data[i].online = false;
				data[i].reputation = 0;
				i++;
			}

			remoteClient.SendDirPeopleReply(queryID, data);
		}
		public void DirEventsQuery(IClientAPI remoteClient, UUID queryID,
				string queryText, uint queryFlags, int queryStart)
		{
			Hashtable ReqHash = new Hashtable();
			ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
			ReqHash["text"] = queryText;
			ReqHash["flags"] = queryFlags.ToString();
			ReqHash["query_start"] = queryStart.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"dir_events_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			} else {
				if(result["timezone"].ToString() != customTimeZone) {
					customTimeZone = result["timezone"].ToString();
					remoteClient.SendAlertMessage("All event times are displayed in the timezone "+customTimeZone);
				}
			}

			ArrayList dataArray = (ArrayList)result["data"];

			int count = dataArray.Count;
			if (count > 100)
				count = 101;

			DirEventsReplyData[] data = new DirEventsReplyData[count];

			int i = 0;

			foreach (Object o in dataArray)
			{
				Hashtable d = (Hashtable)o;

				data[i] = new DirEventsReplyData();
				data[i].ownerID = new UUID(d["owner_id"].ToString());
				data[i].name = d["name"].ToString();
				data[i].eventID = Convert.ToUInt32(d["event_id"]);
				data[i].date = d["date"].ToString();
				data[i].unixTime = Convert.ToUInt32(d["unix_time"]);
				data[i].eventFlags = Convert.ToUInt32(d["event_flags"]);
				i++;
				if (i >= count)
					break;
			}

			remoteClient.SendDirEventsReply(queryID, data);
		}

		public void DirClassifiedQuery(IClientAPI remoteClient, UUID queryID,
				string queryText, uint queryFlags, uint category,
				int queryStart)
		{
			Hashtable ReqHash = new Hashtable();
			ReqHash["text"] = queryText;
			ReqHash["flags"] = queryFlags.ToString();
			ReqHash["category"] = category.ToString();
			ReqHash["query_start"] = queryStart.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"dir_classified_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			}

			ArrayList dataArray = (ArrayList)result["data"];

			int count = dataArray.Count;
			if (count > 100)
				count = 101;

			DirClassifiedReplyData[] data = new DirClassifiedReplyData[count];

			int i = 0;

			foreach (Object o in dataArray)
			{
				Hashtable d = (Hashtable)o;

				data[i] = new DirClassifiedReplyData();
				data[i].classifiedID = new UUID(d["classifiedid"].ToString());
				data[i].name = d["name"].ToString();
				data[i].classifiedFlags = Convert.ToByte(d["classifiedflags"]);
				data[i].creationDate = Convert.ToUInt32(d["creation_date"]);
				data[i].expirationDate = Convert.ToUInt32(d["expiration_date"]);
				data[i].price = Convert.ToInt32(d["priceforlisting"]);
				i++;
				if (i >= count)
					break;
			}

			remoteClient.SendDirClassifiedReply(queryID, data);
		}

		public void EventInfoRequest(IClientAPI remoteClient, uint queryEventID)
		{
			Hashtable ReqHash = new Hashtable();
			ReqHash["avatar_id"] = remoteClient.AgentId.ToString();
			ReqHash["eventID"] = queryEventID.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"event_info_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			}

			ArrayList dataArray = (ArrayList)result["data"];
			if (dataArray.Count == 0)
			{
				// something bad happened here, if we could return an
				// event after the search,
				// we should be able to find it here
				// TODO do some (more) sensible error-handling here
				remoteClient.SendAgentAlertMessage("Couldn't find event.",
						false);
				return;
			}

			Hashtable d = (Hashtable)dataArray[0];
			EventData data = new EventData();
			data.eventID = Convert.ToUInt32(d["event_id"]);
			data.creator = d["creator"].ToString();
			data.name = d["name"].ToString();
			data.category = d["category"].ToString();
			data.description = d["description"].ToString();
			data.date = d["date"].ToString();
			data.dateUTC = Convert.ToUInt32(d["dateUTC"]);
			data.duration = Convert.ToUInt32(d["duration"]);
			data.cover = Convert.ToUInt32(d["covercharge"]);
			data.amount = Convert.ToUInt32(d["coveramount"]);
			data.simName = d["simname"].ToString();
			Vector3.TryParse(d["globalposition"].ToString(), out data.globalPos);
			data.eventFlags = Convert.ToUInt32(d["eventflags"]);

			remoteClient.SendEventInfoReply(data);
		}

		public void ClassifiedInfoRequest(UUID queryClassifiedID, IClientAPI remoteClient)
		{
            if (m_debug)
            {
                m_log.DebugFormat("[{0}] ClassifiedInfoRequest for AgentId {1} for {2}", m_moduleName, remoteClient.AgentId.ToString(), queryClassifiedID.ToString());
            }
			Hashtable ReqHash = new Hashtable();
			ReqHash["classifiedID"] = queryClassifiedID.ToString();

			Hashtable result = GenericXMLRPCRequest(ReqHash,
					"classifieds_info_query");

			if (!Convert.ToBoolean(result["success"]))
			{
				remoteClient.SendAgentAlertMessage(
						result["errorMessage"].ToString(), false);
				return;
			}

			ArrayList dataArray = (ArrayList)result["data"];
			if (dataArray.Count == 0)
			{
				// something bad happened here, if we could return an
				// event after the search,
				// we should be able to find it here
				// TODO do some (more) sensible error-handling here
				// remoteClient.SendAgentAlertMessage("Couldn't find any classifieds.",false);
				return;
			}

			Hashtable d = (Hashtable)dataArray[0];

			Vector3 globalPos = new Vector3();
			Vector3.TryParse(d["posglobal"].ToString(), out globalPos);

			remoteClient.SendClassifiedInfoReply(
					new UUID(d["classifieduuid"].ToString()),
					new UUID(d["creatoruuid"].ToString()),
					Convert.ToUInt32(d["creationdate"]),
					Convert.ToUInt32(d["expirationdate"]),
					Convert.ToUInt32(d["category"]),
					d["name"].ToString(),
					d["description"].ToString(),
					new UUID(d["parceluuid"].ToString()),
					Convert.ToUInt32(d["parentestate"]),
					new UUID(d["snapshotuuid"].ToString()),
					d["simname"].ToString(),
					globalPos,
					d["parcelname"].ToString(),
					Convert.ToByte(d["classifiedflags"]),
					Convert.ToInt32(d["priceforlisting"]));
		}

		public void initDataUpdate(Scene scene, int dataUpdateInterval) {
			Hashtable ReqHash = new Hashtable();

			ReqHash["updateInterval"] = dataUpdateInterval;
			ReqHash["openSimServIP"] = scene.RegionInfo.ServerURI.Replace(scene.RegionInfo.InternalEndPoint.Port.ToString(), 
																				 scene.RegionInfo.HttpPort.ToString());
            if (m_debug) m_log.InfoFormat("[{0}] starting initDataUpdate for {1} width Interval of {2} seconds", m_moduleName, ReqHash["openSimServIP"].ToString(), dataUpdateInterval.ToString());

			Hashtable result = GenericXMLRPCRequest(ReqHash,"init_SearchDataUpdate");

			if (!Convert.ToBoolean(result["success"]))
			{
                m_log.ErrorFormat("[{0}] initDataUpdate returned error: {1}", m_moduleName, result["errorMessage"].ToString());
			}
		}

		public void DataUpdate(Scene scene) {
			Hashtable ReqHash = new Hashtable();

			ReqHash["openSimServIP"] = scene.RegionInfo.ServerURI.Replace(scene.RegionInfo.InternalEndPoint.Port.ToString(), 
																				 scene.RegionInfo.HttpPort.ToString());
			if(forceUpdate == true) {
				ReqHash["forceUpdate"] = "yes";
				forceUpdate = false;
			} else {
				ReqHash["forceUpdate"] = "no";
			}
            if (m_debug) m_log.InfoFormat("[{0}] starting DataUpdate for {1}",m_moduleName, ReqHash["openSimServIP"].ToString());

			Hashtable result = GenericXMLRPCRequest(ReqHash,"searchDataUpdate");

			if (!Convert.ToBoolean(result["success"]))
			{
                m_log.ErrorFormat("[{0}] initDataUpdate returned error: {1}", m_moduleName, result["errorMessage"].ToString());
			}
		}

        public void HandleMapItemRequest(IClientAPI remoteClient, uint flags,
                                         uint EstateID, bool godlike,
                                         uint itemtype, ulong regionhandle)
        {
            //The following constant appears to be from GridLayerType enum
            //defined in OpenMetaverse/GridManager.cs of libopenmetaverse.
            if (itemtype == (uint)OpenMetaverse.GridItemType.LandForSale)
            {
                Hashtable ReqHash = new Hashtable();

                //The flags are: SortAsc (1 << 15), PerMeterSort (1 << 17)
                ReqHash["flags"] = "163840";
                ReqHash["type"] = "4294967295"; //This is -1 in 32 bits
                ReqHash["price"] = "0";
                ReqHash["area"] = "0";
                ReqHash["query_start"] = "0";

                Hashtable result = GenericXMLRPCRequest(ReqHash,
                                                        "dir_land_query");

                if (!Convert.ToBoolean(result["success"]))
                {
                    remoteClient.SendAgentAlertMessage(
                        result["errorMessage"].ToString(), false);
                    return;
                }

                ArrayList dataArray = (ArrayList)result["data"];

                int count = dataArray.Count;
                if (count > 100)
                    count = 101;

                List<mapItemReply> mapitems = new List<mapItemReply>();
                string ParcelRegionUUID;
                string[] landingpoint;

                foreach (Object o in dataArray)
                {
                    Hashtable d = (Hashtable)o;

                    if (d["name"] == null)
                        continue;

                    mapItemReply mapitem = new mapItemReply();

                    ParcelRegionUUID = d["region_UUID"].ToString();

                    foreach (Scene scene in m_Scenes)
                    {
                        if (scene.RegionInfo.RegionID.ToString() == ParcelRegionUUID)
                        {
                            landingpoint = d["landing_point"].ToString().Split('/');

                            mapitem.x = (uint)((scene.RegionInfo.RegionLocX * 256) +
                                                Convert.ToDecimal(landingpoint[0]));
                            mapitem.y = (uint)((scene.RegionInfo.RegionLocY * 256) +
                                                Convert.ToDecimal(landingpoint[1]));
                            break;
                        }
                    }

                    mapitem.id = new UUID(d["parcel_id"].ToString());
                    mapitem.Extra = Convert.ToInt32(d["area"]);
                    mapitem.Extra2 = Convert.ToInt32(d["sale_price"]);
                    mapitem.name = d["name"].ToString();

                    mapitems.Add(mapitem);
                }

                remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                mapitems.Clear();
            }

            if (itemtype == (uint)OpenMetaverse.GridItemType.PgEvent ||
                itemtype == (uint)OpenMetaverse.GridItemType.MatureEvent ||
                itemtype == (uint)OpenMetaverse.GridItemType.AdultEvent)
            {
                Hashtable ReqHash = new Hashtable();

                //Find the maturity level
                int maturity = (1 << 24);

                //Find the maturity level
                if (itemtype == (uint)OpenMetaverse.GridItemType.MatureEvent)
                    maturity = (1 << 25);
                else
                {
                    if (itemtype == (uint)OpenMetaverse.GridItemType.AdultEvent)
                        maturity = (1 << 26);
                }

                //The flags are: SortAsc (1 << 15), PerMeterSort (1 << 17)
                maturity |= 163840;

                //Character before | is number of days before/after current date
                //Characters after | is the number for a category
                ReqHash["text"] = "0|0|";
                ReqHash["flags"] = maturity.ToString();
                ReqHash["query_start"] = "0";

                Hashtable result = GenericXMLRPCRequest(ReqHash,
                                                        "dir_events_query");

                if (!Convert.ToBoolean(result["success"]))
                {
                    remoteClient.SendAgentAlertMessage(
                        result["errorMessage"].ToString(), false);
                    return;
                }

                ArrayList dataArray = (ArrayList)result["data"];

                List<mapItemReply> mapitems = new List<mapItemReply>();
                string ParcelRegionUUID;
                string[] landingpoint;

                foreach (Object o in dataArray)
                {
                    Hashtable d = (Hashtable)o;

                    if (d["name"] == null)
                        continue;

                    mapItemReply mapitem = new mapItemReply();

                    ParcelRegionUUID = d["region_UUID"].ToString();

                    foreach (Scene scene in m_Scenes)
                    {
                        if (scene.RegionInfo.RegionID.ToString() == ParcelRegionUUID)
                        {
                            landingpoint = d["landing_point"].ToString().Split('/');

                            mapitem.x = (uint)((scene.RegionInfo.RegionLocX * 256) +
                                                Convert.ToDecimal(landingpoint[0]));
                            mapitem.y = (uint)((scene.RegionInfo.RegionLocY * 256) +
                                                Convert.ToDecimal(landingpoint[1]));
                            break;
                        }
                    }

                    mapitem.id = UUID.Random();
                    mapitem.Extra = (int)Convert.ToInt32(d["unix_time"]);
                    mapitem.Extra2 = (int)Convert.ToInt32(d["event_id"]);
                    mapitem.name = d["name"].ToString();

                    mapitems.Add(mapitem);
                }

                remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                mapitems.Clear();
            }
        }
	}
}

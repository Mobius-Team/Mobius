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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using System.Collections.Generic;

namespace OpenSim.Capabilities.Handlers
{
    public class ViewerAssetHandler
    {
		// Only allow the types the viewer should be allowed to receive
        Dictionary<string, AssetType> paramToAssetType = new Dictionary<string, AssetType>()
        {
            {"texture_id",      AssetType.Texture },
            {"sound_id",        AssetType.Sound },
            //{"callcard_id",     AssetType.CallingCard},
            {"landmark_id",     AssetType.Landmark },
		    //{"script_id",     AssetType.Script}, // deprecated
		    {"clothing_id",     AssetType.Clothing },
            //{"object_id",       AssetType.Object},
            //{"notecard_id",     AssetType.Notecard},
            //{"category_id",     AssetType.Folder},
            //{"lsltext_id",      AssetType.LSLText},
            //{"lslbyte_id",      AssetType.LSLBytecode},
            //{"txtr_tga_id",     AssetType.TextureTGA},
            {"bodypart_id",     AssetType.Bodypart },
            //{"snd_wav_id",      AssetType.SoundWAV },
            //{"img_tga_id",      AssetType.ImageTGA},
            //{"jpeg_id",         AssetType.ImageJPEG },
		    {"animatn_id",      AssetType.Animation },
            {"gesture_id",      AssetType.Gesture },
            {"simstate_id",     AssetType.Simstate },
            {"link_id",         AssetType.Link },
            {"link_f_id",       AssetType.LinkFolder },
            {"mesh_id",         AssetType.Mesh }
        };

        Dictionary<AssetType, string> assetTypeToContentType = new Dictionary<AssetType, string>()
        {
            { AssetType.Texture,    "image/x-j2c" },
            { AssetType.Sound,      "application/ogg" },
            { AssetType.Landmark,   "application/vnd.ll.landmark" },
            { AssetType.Clothing,   "application/vnd.ll.clothing" },
            { AssetType.Bodypart,   "application/vnd.ll.bodypart" },
            { AssetType.Animation,  "application/vnd.ll.animation" },
            { AssetType.Gesture,    "application/vnd.ll.gesture" },
            { AssetType.Mesh,       "application/vnd.ll.mesh" },
        };

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetService m_assetService;

        private string m_ExternalAssetsURL = "";

        public ViewerAssetHandler(IAssetService assService, string externalAssetsURL = "")
        {
            m_assetService = assService;
			m_ExternalAssetsURL = externalAssetsURL;
        }

        public Hashtable Handle(Hashtable request)
        {
            Hashtable ret = new Hashtable();
            ret["int_response_code"] = (int)System.Net.HttpStatusCode.NotFound;
            ret["content_type"] = "text/plain";
            ret["int_bytes"] = 0;
            
            string[] keys = new string[request.Keys.Count];
            request.Keys.CopyTo(keys, 0);

            AssetType assetType = AssetType.Unknown;

            UUID assetID = UUID.Zero;
            string assetStr = string.Empty;

            foreach(string param in paramToAssetType.Keys)
            {
                if(request.ContainsKey(param))
                {
                    assetStr = param;
                    assetType = paramToAssetType[param];

                    if (!UUID.TryParse((string)request[param], out assetID))
                    {
                        //m_log.Debug("[ViewerAsset]: Received request with malformed asset id");
                    }
                    break;
                }
            }

            //m_log.DebugFormat("[ViewerAsset]: called {0}", assetStr);

            if (m_assetService == null)
            {
                m_log.Error("[ViewerAsset]: Cannot fetch asset " + assetStr + " without an asset service");
            }

            if(assetType == AssetType.Unknown || assetID == UUID.Zero)
            {
                ret["int_response_code"] = 404;
                ret["error_status_text"] = "Incorrect Syntax";
                ret["str_response_string"] = "Incorrect Syntax";
                ret["content_type"] = "text/plain";
                ret["int_bytes"] = 0;
                return ret;
            }
            
            //m_log.DebugFormat("[ViewerAsset]: Received request for asset id {0} as type {1}", assetID, assetType);
            if (!FetchAsset(request, ret, assetID, assetType, assetStr))
            {
                ret["int_response_code"] = 404;
                ret["error_status_text"] = "Not found!";
                ret["str_response_string"] = "Not found!";
                ret["content_type"] = "text/plain";
                ret["int_bytes"] = 0;
            }
            return ret;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <param name="assetID"></param>
        /// <param name="assetType"></param>
        /// <returns>False for "caller try another codec"; true otherwise</returns>
        private bool FetchAsset(Hashtable request, Hashtable response, UUID assetID, AssetType assetType, string assetStr)
        {
            //m_log.DebugFormat("[ViewerAsset]: {0} with requested type {1}", assetID, assetType);
            string fullID = assetID.ToString();

            // try the cache
            AssetBase asset = m_assetService.GetCached(fullID);

            if (asset == null)
            {
                if (m_ExternalAssetsURL != "")
                {
                    response["int_response_code"] = 301;
                    response["str_redirect_location"] = string.Format("{0}/?{1}={2}", m_ExternalAssetsURL, assetStr, assetID);
                    response["content_type"] = "text/plain";
                    response["keepalive"] = false;
                    return true;
                }


                //m_log.DebugFormat("[ViewerAsset]: Asset was not in the cache!");

                // Fetch locally or remotely. Misses return a 404
                asset = m_assetService.Get(assetID.ToString());

                if (asset != null)
                {
                    if (asset.Type != (sbyte)assetType)
                        return false;

                    WriteAssetData(request, response, asset);
                    return true;
                }
                else return false;
            }
            else
            {
                WriteAssetData(request, response, asset);
                return true;
            }
        }

        private void WriteAssetData(Hashtable request, Hashtable response, AssetBase asset)
        {
            Hashtable headers = new Hashtable();
            response["headers"] = headers;

            string range = String.Empty;

            if (((Hashtable)request["headers"])["range"] != null)
                range = (string)((Hashtable)request["headers"])["range"];

            else if (((Hashtable)request["headers"])["Range"] != null)
                range = (string)((Hashtable)request["headers"])["Range"];

            if (!String.IsNullOrEmpty(range) && asset.Type == (sbyte)AssetType.Texture) // JP2's only
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    // Before clamping start make sure we can satisfy it in order to avoid
                    // sending back the last byte instead of an error status
                    if (start >= asset.Data.Length)
                    {
                        response["int_response_code"] = (int)System.Net.HttpStatusCode.NotFound;
                    }
                    else
                    {
                        // Handle the case where no second range value was given.  This is equivalent to requesting
                        // the rest of the entity.
                        if (end == -1)
                            end = int.MaxValue;

                        end = Utils.Clamp(end, 0, asset.Data.Length - 1);
                        start = Utils.Clamp(start, 0, end);
                        int len = end - start + 1;

                        //m_log.Debug("Serving " + start + " to " + end + " of " + asset.Data.Length + " bytes for texture " + asset.ID);

                        response["content-type"] = asset.Metadata.ContentType;
                        response["int_response_code"] = (int)System.Net.HttpStatusCode.PartialContent;
                        headers["Content-Range"] = String.Format("bytes {0}-{1}/{2}", start, end, asset.Data.Length);

                        byte[] d = new byte[len];
                        Array.Copy(asset.Data, start, d, 0, len);
                        response["bin_response_data"] = d;
                        response["int_bytes"] = len;
                    }
                }
                else
                {
                    m_log.Warn("[ViewerAsset]: Malformed Range header: " + range);
                    response["int_response_code"] = (int)System.Net.HttpStatusCode.BadRequest;
                }
            }
            else // JP2's or other formats
            {
                // Full content request
                response["int_response_code"] = (int)System.Net.HttpStatusCode.OK;
                
                string content_type = string.Empty;
                if(!assetTypeToContentType.TryGetValue((AssetType)asset.Type, out content_type))
                    content_type = "application/octet-stream";
                response["content_type"] = content_type;

                response["bin_response_data"] = asset.Data;
                response["int_bytes"] = asset.Data.Length;
            }
        }

        /// <summary>
        /// Parse a range header.
        /// </summary>
        /// <remarks>
        /// As per http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html,
        /// this obeys range headers with two values (e.g. 533-4165) and no second value (e.g. 533-).
        /// Where there is no value, -1 is returned.
        /// FIXME: Need to cover the case where only a second value is specified (e.g. -4165), probably by returning -1
        /// for start.</remarks>
        /// <returns></returns>
        /// <param name='header'></param>
        /// <param name='start'>Start of the range.  Undefined if this was not a number.</param>
        /// <param name='end'>End of the range.  Will be -1 if no end specified.  Undefined if there was a raw string but this was not a number.</param>
        private bool TryParseRange(string header, out int start, out int end)
        {
            start = end = 0;

            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');

                if (rangeValues.Length == 2)
                {
                    if (!Int32.TryParse(rangeValues[0], out start))
                        return false;

                    string rawEnd = rangeValues[1];

                    if (rawEnd == "")
                    {
                        end = -1;
                        return true;
                    }
                    else if (Int32.TryParse(rawEnd, out end))
                    {
                        return true;
                    }
                }
            }

            start = end = 0;
            return false;
        }
    }
}

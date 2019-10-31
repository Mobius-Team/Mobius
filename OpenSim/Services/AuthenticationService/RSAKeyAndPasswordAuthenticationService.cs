using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Services.Interfaces;
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Linq;

namespace OpenSim.Services.AuthenticationService
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need
    // verifiable identification.
    //
    public class RSAKeyAndPasswordAuthenticationService :
            PasswordAuthenticationService, IAuthenticationService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public RSAKeyAndPasswordAuthenticationService(IConfigSource config, IUserAccountService userService) :
                base(config, userService)
        {
            m_log.Debug("[RSA AUTH SERVICE]: Started with User Account access");
        }

        public RSAKeyAndPasswordAuthenticationService(IConfigSource config) :
                base(config)
        {
        }

        new public string Authenticate(UUID principalID, string password, int lifetime, out UUID realID)
        {
            realID = UUID.Zero;

            m_log.DebugFormat("[AUTH SERVICE]: Authenticating for {0}, user account service present: {1}", principalID, m_UserAccountService != null);
            AuthenticationData data = m_Database.Get(principalID);
            UserAccount user = null;
            if (m_UserAccountService != null)
                user = m_UserAccountService.GetUserAccount(UUID.Zero, principalID);

            if (data == null || data.Data == null)
            {
                m_log.DebugFormat("[AUTH SERVICE]: PrincipalID {0} or its data not found", principalID);
                return String.Empty;
            }

            if(data.Data.ContainsKey("rsaOnly"))
            {
                if(data.Data["rsaOnly"] != null)
                {
                    if(data.Data["rsaOnly"].ToString() == "1")
                    {
                        return "requires_rsa";
                    }
                }
            }

            if (!data.Data.ContainsKey("passwordHash") ||
                !data.Data.ContainsKey("passwordSalt"))
            {
                return String.Empty;
            }

            string hashed = Util.Md5Hash(password + ":" +
                    data.Data["passwordSalt"].ToString());

//            m_log.DebugFormat("[PASS AUTH]: got {0}; hashed = {1}; stored = {2}", password, hashed, data.Data["passwordHash"].ToString());

            if (data.Data["passwordHash"].ToString() == hashed)
            {
                return GetToken(principalID, lifetime);
            }

            if (user == null)
            {
                m_log.DebugFormat("[PASS AUTH]: No user record for {0}", principalID);
                return String.Empty;
            }

            int impersonateFlag = 1 << 6;

            if ((user.UserFlags & impersonateFlag) == 0)
                return String.Empty;

            m_log.DebugFormat("[PASS AUTH]: Attempting impersonation");

            List<UserAccount> accounts = m_UserAccountService.GetUserAccountsWhere(UUID.Zero, "UserLevel >= 200");
            if (accounts == null || accounts.Count == 0)
                return String.Empty;

            foreach (UserAccount a in accounts)
            {
                data = m_Database.Get(a.PrincipalID);
                if (data == null || data.Data == null ||
                    !data.Data.ContainsKey("passwordHash") ||
                    !data.Data.ContainsKey("passwordSalt"))
                {
                    continue;
                }

//                m_log.DebugFormat("[PASS AUTH]: Trying {0}", data.PrincipalID);

                hashed = Util.Md5Hash(password + ":" +
                        data.Data["passwordSalt"].ToString());

                if (data.Data["passwordHash"].ToString() == hashed)
                {
                    m_log.DebugFormat("[PASS AUTH]: {0} {1} impersonating {2}, proceeding with login", a.FirstName, a.LastName, principalID);
                    realID = a.PrincipalID;
                    return GetToken(principalID, lifetime);
                }
//                else
//                {
//                    m_log.DebugFormat(
//                        "[AUTH SERVICE]: Salted hash {0} of given password did not match salted hash of {1} for PrincipalID {2}.  Authentication failure.",
//                        hashed, data.Data["passwordHash"], data.PrincipalID);
//                }
            }

            m_log.DebugFormat("[PASS AUTH]: Impersonation of {0} failed", principalID);
            return String.Empty;
        }

        private struct LoginData
        {
            public string password;
            public RSAParameters rsa_params;
            public int lifetime;
        }
        
        private Dictionary<UUID, LoginData> userToRSAParams = new Dictionary<UUID, LoginData>();
        
        new public bool RSAAuthenticate(UUID principalID, int lifetime, out string magic, out string key)
        {
            m_log.DebugFormat("[RSA AUTH SERVICE]: Authentication request for {0}", principalID);
            AuthenticationData data = m_Database.Get(principalID);
            UserAccount user = null;
            if (m_UserAccountService != null)
                user = m_UserAccountService.GetUserAccount(UUID.Zero, principalID);

            if (data == null || data.Data == null)
            {
                m_log.DebugFormat("[RSA AUTH SERVICE]: PrincipalID {0} or its data not found", principalID);
                magic = key = "";
                return false;
            }

            if(!data.Data.ContainsKey("rsaKey") || string.IsNullOrWhiteSpace(data.Data["rsaKey"]?.ToString()))
            {
                m_log.DebugFormat("[RSA AUTH SERVICE]: RSA Key for {0} is null.", principalID);
                magic = key = "";
                return false;
            }

            try
            {
                byte[] publicKey = Convert.FromBase64String((string)data.Data["rsaKey"]);
                
                var csp = PemKeyUtils.DecodeX509PublicKey( publicKey );

                RSACryptoServiceProvider one_off_rsa = new RSACryptoServiceProvider();

                var privKey = one_off_rsa.ExportParameters(true);

                if(userToRSAParams.ContainsKey(principalID))
                {
                    userToRSAParams.Remove(principalID);
                }

                LoginData login_data = new LoginData();
                login_data.password = Utils.MD5String(Utils.RandomDouble().ToString());;
                login_data.rsa_params = privKey;
                login_data.lifetime = lifetime;

                userToRSAParams.Add(principalID, login_data);

                string pubKeyString = PemKeyUtils.GetPublicKey(one_off_rsa);

                var bytesPlainTextData = System.Text.Encoding.UTF8.GetBytes(login_data.password);

                var bytesCypherText = csp.Encrypt(bytesPlainTextData, false);

                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(pubKeyString); 

                magic = Convert.ToBase64String(bytesCypherText);
                key = Convert.ToBase64String(plainTextBytes);

                return true;
            }
            catch
            {
                m_log.DebugFormat("[RSA AUTH SERVICE]: There was an exception starting the RSA login for {0}. Possibly due to an invalid key.", principalID);
                magic = key = "";
                return false;
            }
        }

        string ConvertWithoutEncoding(byte[] data)
        {
            char[] characters = data.Select(b => (char)b).ToArray();
            return new string(characters);
        }

        new public bool FinishRSALogin(UUID principalID, string data, out string token)
        {
            LoginData login_data;
            if(!userToRSAParams.TryGetValue(principalID, out login_data))
            {
                token = "";
                return false;
            }

            userToRSAParams.Remove(principalID);

            try
            {
                RSACryptoServiceProvider csp = new RSACryptoServiceProvider();
                csp.ImportParameters(login_data.rsa_params);

                byte[] bytesCypherText = Convert.FromBase64String(data);

                byte[] bytesPlainTextData = csp.Decrypt(bytesCypherText, false);

                string plainTextData = ConvertWithoutEncoding(bytesPlainTextData);

                if(plainTextData == login_data.password)
                {
                    token = GetToken(principalID, login_data.lifetime);
                    return true;
                }
            }
            catch
            {
                m_log.DebugFormat("[RSA AUTH SERVICE]: There was an exception finishing the RSA login for {0}. Possibly due to an invalid key.", principalID);
            }


            token = "";
            return false;
        }

        new public bool SetPublicKey(UUID principalID, string public_key)
        {
            AuthenticationData auth = m_Database.Get(principalID);
            if (auth == null)
            {
                return false;
            }

            auth.Data["rsaKey"] = public_key;
            if (!m_Database.Store(auth))
            {
                m_log.DebugFormat("[AUTHENTICATION DB]: Failed to store authentication data");
                return false;
            }

            m_log.InfoFormat("[AUTHENTICATION DB]: Set rsaKey for principalID {0}", principalID);

            return true;
        }

        new public bool EnforceRSALogin(UUID principalID, bool enforce)
        {
            AuthenticationData auth = m_Database.Get(principalID);
            if (auth == null)
            {
                return false;
            }

            auth.Data["rsaOnly"] = enforce ? 1 : 0;
            if (!m_Database.Store(auth))
            {
                m_log.DebugFormat("[AUTHENTICATION DB]: Failed to store authentication data");
                return false;
            }

            m_log.InfoFormat("[AUTHENTICATION DB]: Password login {0} for principalID {1}", enforce ? "disabled" : "enabled", principalID);

            return true;
        }
    }
}

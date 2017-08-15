using OpenSim.Region.ScriptEngine.Shared;
using System.Collections.Generic;

namespace SecondLife
{
    public class XEngineScript : OpenSim.Region.ScriptEngine.XEngine.ScriptBase.XEngineScriptBase
    {
        public XEngineScript(System.Threading.WaitHandle coopSleepHandle) : base(coopSleepHandle) {}

        public void default_event_state_entry()
        {
            opensim_reserved_CheckForCoopTermination();
            llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString("Thin Lizzy"));
            llSleep(new LSL_Types.LSLInteger(60));
        }
    }
}

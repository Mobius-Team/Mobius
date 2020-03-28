namespace OpenSim.Data
{
    public interface IAccessControlData
    {
        bool IsHardwareBanned(string mac, string id0);
        bool IsIPBanned(string ip);
    }
}

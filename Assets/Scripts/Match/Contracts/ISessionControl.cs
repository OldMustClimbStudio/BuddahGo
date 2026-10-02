namespace BuddahGo.Match
{
    public interface ISessionControl
    {
        bool IsOnlineAvailable { get; }
        bool IsStarting { get; }
        string LastError { get; }
        bool StartOnlineHost();
        bool StartOnlineClient(string hostSteamId);
        bool StartSoloHost(SoloMatchSettings settings);
        void RequestStopSession();
        SoloMatchSettings TakeFailedSoloSettings();
    }

    public static class SessionControl
    {
        public static SoloMatchSettings SoloSettings { get; internal set; }
        public static ISessionControl Current { get; set; }
    }
}

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
        // Stops the running session (an online room the host is alone in) and starts this solo race instead,
        // without going back through the menu scene.
        void QueueSoloAfterStop(SoloMatchSettings settings);
        SoloMatchSettings TakeFailedSoloSettings();
    }

    public static class SessionControl
    {
        public static SoloMatchSettings SoloSettings { get; internal set; }
        public static ISessionControl Current { get; set; }
    }
}

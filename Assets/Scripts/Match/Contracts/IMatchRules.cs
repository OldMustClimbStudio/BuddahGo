namespace BuddahGo.Match
{
    public enum MatchReturnTarget { Room, MainMenuHome }

    public interface IMatchRules
    {
        bool AutoStartRoom { get; }
        bool RequiresReady { get; }
        bool SelectionTimeoutEnabled { get; }
        bool SkipMapVote { get; }
        bool VoteOnResults { get; }
        bool ResultDecisionTimeoutEnabled { get; }
        MatchReturnTarget ReturnTarget { get; }
        bool EndWhenAllRacersFinished { get; }
        bool EndOnHumanFinish { get; }
        bool AllowSkipSpectating { get; }
        bool AllowQuitDialog { get; }
        bool AllowPause { get; }
        bool ShowRacerNameTags { get; }
        bool ShowOpponentsOnMinimap { get; }
        string DefaultPlayerName { get; }
    }
}

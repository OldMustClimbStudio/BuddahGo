namespace BuddahGo.Match
{
    public enum MatchReturnTarget { Room, MainMenuHome }

    public interface IMatchRules
    {
        // Solo: one local host whose server also drives the AI racers. Gates Solo-only spawn,
        // presentation-clock and intro paths; ReturnTarget only decides where a session returns.
        bool IsSolo { get; }
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

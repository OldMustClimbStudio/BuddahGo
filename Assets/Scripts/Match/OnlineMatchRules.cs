namespace BuddahGo.Match
{
    public sealed class OnlineMatchRules : IMatchRules
    {
        public bool IsSolo => false;
        public bool AutoStartRoom => false;
        public bool RequiresReady => true;
        public bool SelectionTimeoutEnabled => true;
        public bool SkipMapVote => false;
        public bool VoteOnResults => true;
        public bool ResultDecisionTimeoutEnabled => true;
        public MatchReturnTarget ReturnTarget => MatchReturnTarget.Room;
        public bool EndWhenAllRacersFinished => false;
        public bool EndOnHumanFinish => false;
        public bool AllowSkipSpectating => false;
        public bool AllowQuitDialog => false;
        public bool AllowPause => false;
        public bool ShowRacerNameTags => false;
        public bool ShowOpponentsOnMinimap => false;
        public string DefaultPlayerName => null;
    }
}

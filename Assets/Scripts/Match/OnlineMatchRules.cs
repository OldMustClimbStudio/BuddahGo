namespace BuddahGo.Match
{
    // Values come from Match/ModeRule (row "Online"); the literals are the fallback when no table row exists.
    public sealed class OnlineMatchRules : IMatchRules
    {
        private ModeRuleRecord R => DesignTableOverlay.ModeRule("Online");
        public bool IsSolo => false;
        public bool AutoStartRoom => R?.autoStartRoom ?? false;
        public bool RequiresReady => R?.requiresReady ?? true;
        public bool SelectionTimeoutEnabled => R?.selectionTimeoutEnabled ?? true;
        public bool SkipMapVote => R?.skipMapVote ?? false;
        public bool VoteOnResults => R?.voteOnResults ?? true;
        public bool ResultDecisionTimeoutEnabled => R?.resultDecisionTimeoutEnabled ?? true;
        public MatchReturnTarget ReturnTarget => R != null && R.returnTarget == "MainMenuHome" ? MatchReturnTarget.MainMenuHome : MatchReturnTarget.Room;
        public bool EndWhenAllRacersFinished => R?.endWhenAllRacersFinished ?? false;
        public bool EndOnHumanFinish => R?.endOnHumanFinish ?? false;
        public bool SpawnTrainingDummies => false;
        public bool AllowSkipSpectating => R?.allowSkipSpectating ?? false;
        public bool AllowQuitDialog => R?.allowQuitDialog ?? false;
        public bool AllowPause => R?.allowPause ?? false;
        public bool ShowRacerNameTags => R?.showRacerNameTags ?? false;
        public bool ShowOpponentsOnMinimap => R?.showOpponentsOnMinimap ?? false;
        public string DefaultPlayerName => R != null && !string.IsNullOrEmpty(R.defaultPlayerNameKey) ? DesignTableOverlay.Text(R.defaultPlayerNameKey, null) : null;
    }
}

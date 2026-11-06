using System;

namespace BuddahGo.Match
{
    // Values come from Match/ModeRule (row "Solo", or "Practice" when there are no AI racers);
    // the literals are the fallback when no table row exists.
    public sealed class SoloMatchRules : IMatchRules
    {
        private readonly int _aiCount;
        public SoloMatchRules(SoloMatchSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _aiCount = settings.AICount;
        }
        private ModeRuleRecord R => DesignTableOverlay.ModeRule(_aiCount == 0 ? "Practice" : "Solo") ?? DesignTableOverlay.ModeRule("Solo");
        public bool IsSolo => true;
        public bool AutoStartRoom => R?.autoStartRoom ?? true;
        public bool RequiresReady => R?.requiresReady ?? false;
        public bool SelectionTimeoutEnabled => R?.selectionTimeoutEnabled ?? false;
        public bool SkipMapVote => R?.skipMapVote ?? true;
        public bool VoteOnResults => R?.voteOnResults ?? false;
        public bool ResultDecisionTimeoutEnabled => R?.resultDecisionTimeoutEnabled ?? false;
        public MatchReturnTarget ReturnTarget => R != null && R.returnTarget == "Room" ? MatchReturnTarget.Room : MatchReturnTarget.MainMenuHome;
        public bool EndWhenAllRacersFinished => R?.endWhenAllRacersFinished ?? true;
        public bool EndOnHumanFinish => _aiCount == 0;
        public bool SpawnTrainingDummies => _aiCount == 0; // Practice only; the count and layout are the Match/ModeRule Practice row.
        public bool AllowSkipSpectating => R?.allowSkipSpectating ?? false; // S6
        public bool AllowQuitDialog => R?.allowQuitDialog ?? true;
        public bool AllowPause => R?.allowPause ?? false; // S7
        public bool ShowRacerNameTags => R?.showRacerNameTags ?? false; // S6
        public bool ShowOpponentsOnMinimap => R?.showOpponentsOnMinimap ?? false; // S6
        public string DefaultPlayerName => DesignTableOverlay.Text(R?.defaultPlayerNameKey, "玩家");
    }
}

using System;

namespace BuddahGo.Match
{
    public sealed class SoloMatchRules : IMatchRules
    {
        private readonly int _aiCount;
        public SoloMatchRules(SoloMatchSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _aiCount = settings.AICount;
        }
        public bool IsSolo => true;
        public bool AutoStartRoom => true;
        public bool RequiresReady => false;
        public bool SelectionTimeoutEnabled => false;
        public bool SkipMapVote => true;
        public bool VoteOnResults => false;
        public bool ResultDecisionTimeoutEnabled => false;
        public MatchReturnTarget ReturnTarget => MatchReturnTarget.MainMenuHome;
        public bool EndWhenAllRacersFinished => true;
        public bool EndOnHumanFinish => _aiCount == 0;
        public bool AllowSkipSpectating => false; // S6
        public bool AllowQuitDialog => true;
        public bool AllowPause => false; // S7
        public bool ShowRacerNameTags => false; // S6
        public bool ShowOpponentsOnMinimap => false; // S6
        public string DefaultPlayerName => "玩家";
    }
}

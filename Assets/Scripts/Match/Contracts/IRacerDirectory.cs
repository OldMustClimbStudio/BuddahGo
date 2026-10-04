using System.Collections.Generic;

namespace BuddahGo.Match
{
    public interface IRacerDirectory
    {
        IReadOnlyCollection<RacerIdentity> All { get; }
        bool TryGet(RacerId id, out RacerIdentity racer);
        void Register(RacerIdentity racer);
        void Unregister(RacerIdentity racer);
    }
    public static class RacerDirectory
    {
        public static IRacerDirectory Current { get; set; }
    }
}

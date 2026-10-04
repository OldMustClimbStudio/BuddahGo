namespace BuddahGo.Match
{
    public interface IMatchClock
    {
        double Now { get; }
        bool IsPaused { get; }
    }
}

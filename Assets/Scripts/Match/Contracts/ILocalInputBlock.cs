namespace BuddahGo.Match
{
    public interface ILocalInputBlock { bool IsBlocked { get; } }

    public static class LocalInputBlock
    {
        public static ILocalInputBlock Current { get; set; }
        public static bool IsBlocked => Current != null && Current.IsBlocked;
    }
}

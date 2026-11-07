namespace BuddahGo.Tests
{
    /// <summary>Concrete 缘起 for EditMode tests: records the calls, has no effect.</summary>
    public sealed class YuanqiTestAction : YuanqiAction
    {
        public int TriggerCount;
        public int EndCount;

        public override void OnTriggerServer(YuanqiContext ctx) => TriggerCount++;
        public override void OnEndServer(YuanqiContext ctx) => EndCount++;
    }
}

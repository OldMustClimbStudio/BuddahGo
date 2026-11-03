using System;
using System.Linq;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class RacerIdTests
    {
        [Test]
        public void HumanAndAIIdsStayInSeparateRanges()
        {
            EveryCase.Parts(
                ("human ids preserve the connection identity without entering the reserved AI interval", () =>
                    EveryCase.Check(new[] { 0, 9999 }, id =>
                    {
                        var racer = RacerId.FromClient(id);
                        Assert.That(racer.Value, Is.EqualTo(id));
                        Assert.That(racer.IsAI, Is.False);
                    }, id => "id=" + id)),
                ("invalid client ids cannot collide with AI", () =>
                    EveryCase.Check(new[] { -1, 10000, int.MaxValue }, id =>
                        Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.FromClient(id)), id => "id=" + id)),
                ("the wire identity rejects invalid values", () =>
                    EveryCase.Check(new[] { -1, 10005 }, id =>
                        Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.FromValue(id)), id => "id=" + id)),
                ("AI ids are unique and outside the human namespace", () =>
                {
                    var ids = Enumerable.Range(0, 5).Select(RacerId.ForAI).ToArray();
                    Assert.That(ids.Distinct().Count(), Is.EqualTo(5));
                    Assert.That(ids.All(x => x.IsAI && x.Value >= 10000), Is.True);
                    Assert.That(RacerId.FromClient(0).IsAI, Is.False);
                    Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.ForAI(-1));
                    Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.ForAI(5));
                }));
        }
    }
}

using System;
using System.Linq;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class RacerIdTests
    {
        [TestCase(0), TestCase(9999)]
        public void HumanIdsPreserveConnectionIdentityWithoutEnteringReservedAIInterval(int id)
        {
            var racer = RacerId.FromClient(id);
            Assert.That(racer.Value, Is.EqualTo(id));
            Assert.That(racer.IsAI, Is.False);
        }

        [TestCase(-1), TestCase(10000), TestCase(int.MaxValue)]
        public void InvalidClientIdsCannotCollideWithAI(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.FromClient(id));
        }

        [TestCase(-1), TestCase(10005)]
        public void WireIdentityRejectsInvalidValues(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.FromValue(id));
        }

        [Test]
        public void AIIdsAreUniqueAndOutsideHumanNamespace()
        {
            var ids = Enumerable.Range(0, 5).Select(RacerId.ForAI).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(5));
            Assert.That(ids.All(x => x.IsAI && x.Value >= 10000), Is.True);
            Assert.That(RacerId.FromClient(0).IsAI, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.ForAI(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => RacerId.ForAI(5));
        }
    }
}

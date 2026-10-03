using System;
using System.Collections.Generic;
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

        [Test]
        public void ValueEqualityAllowsIndependentLookupsAndDistinctRacers()
        {
            var racer = RacerId.FromClient(7);
            var equivalent = RacerId.FromClient(7);
            var other = RacerId.FromClient(8);
            var entries = new Dictionary<RacerId, string> { [racer] = "human" };
            Assert.That(racer == equivalent, Is.True);
            Assert.That(racer != other, Is.True);
            Assert.That(racer.Equals((object)equivalent), Is.True);
            Assert.That(racer.Equals(null), Is.False);
            Assert.That(racer.GetHashCode(), Is.EqualTo(equivalent.GetHashCode()));
            Assert.That(entries[equivalent], Is.EqualTo("human"));
            Assert.That(entries.ContainsKey(other), Is.False);
        }
    }
}

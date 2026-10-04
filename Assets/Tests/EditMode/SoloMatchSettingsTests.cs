using System;
using System.Collections.Generic;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class SoloMatchSettingsTests
    {
        [Test]
        public void NamesAreAnImmutableCopyOfTheRequest()
        {
            var names = new List<string> { "A", "B" };
            var settings = new SoloMatchSettings(2, SoloDifficulty.Hard, names);
            names[0] = "changed";
            names.Add("C");
            Assert.That(settings.AICount, Is.EqualTo(2));
            Assert.That(settings.Difficulty, Is.EqualTo(SoloDifficulty.Hard));
            Assert.That(settings.AINames, Is.EqualTo(new[] { "A", "B" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)settings.AINames)[0] = "changed");
        }

        [TestCase(0), TestCase(5)]
        public void ValidBoundaryCountsAllowMissingNames(int count)
        {
            var settings = new SoloMatchSettings(count, SoloDifficulty.Normal);
            Assert.That(settings.AICount, Is.EqualTo(count));
            Assert.That(settings.AINames, Is.Empty);
        }

        [TestCase(-1), TestCase(6)]
        public void RejectsUnsupportedRacerCounts(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoloMatchSettings(count, SoloDifficulty.Normal));
        }

        [Test]
        public void RejectsUnknownDifficulty()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoloMatchSettings(0, (SoloDifficulty)99));
        }
    }
}

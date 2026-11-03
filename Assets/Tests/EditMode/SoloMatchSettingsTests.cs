using System;
using System.Collections.Generic;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class SoloMatchSettingsTests
    {
        [Test]
        public void SettingsCopyTheNamesAndRejectUnsupportedValues()
        {
            EveryCase.Parts(
                ("names are an immutable copy of the request", () =>
                {
                    var names = new List<string> { "A", "B" };
                    var settings = new SoloMatchSettings(2, SoloDifficulty.Hard, names);
                    names[0] = "changed";
                    names.Add("C");
                    Assert.That(settings.AICount, Is.EqualTo(2));
                    Assert.That(settings.Difficulty, Is.EqualTo(SoloDifficulty.Hard));
                    Assert.That(settings.AINames, Is.EqualTo(new[] { "A", "B" }));
                    Assert.Throws<NotSupportedException>(() => ((IList<string>)settings.AINames)[0] = "changed");
                }),
                ("valid boundary counts allow missing names", () =>
                    EveryCase.Check(new[] { 0, 5 }, count =>
                    {
                        var settings = new SoloMatchSettings(count, SoloDifficulty.Normal);
                        Assert.That(settings.AICount, Is.EqualTo(count));
                        Assert.That(settings.AINames, Is.Empty);
                    }, count => "count=" + count)),
                ("rejects unsupported racer counts", () =>
                    EveryCase.Check(new[] { -1, 6 }, count =>
                        Assert.Throws<ArgumentOutOfRangeException>(() => new SoloMatchSettings(count, SoloDifficulty.Normal)),
                        count => "count=" + count)),
                ("rejects an unknown difficulty", () =>
                    Assert.Throws<ArgumentOutOfRangeException>(() => new SoloMatchSettings(0, (SoloDifficulty)99))));
        }
    }
}

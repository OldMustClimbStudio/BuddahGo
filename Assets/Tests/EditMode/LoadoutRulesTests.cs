using System.Collections.Generic;
using NUnit.Framework;
using SteamMultiplayer.Network;

namespace BuddahGo.Tests
{
    public class LoadoutRulesTests
    {
        private static bool Valid(string id) => id == "a" || id == "b" || id == "c";

        [Test]
        public void SubmissionTrimsAndAllowsBlankSlotsForAutofill()
        {
            var ids = new[] { " a ", "", (string)null };
            Assert.That(LoadoutRules.ValidateSubmission(ids, 3, Valid, () => false, out var error), Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(ids, Is.EqualTo(new[] { "a", "", "" }));
            Assert.That(LoadoutRules.HasCompleteSelection(ids, Valid, () => false), Is.False);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void DuplicatePolicyAppliesToSubmissionAndCompletion(bool allow, bool expected)
        {
            var ids = new[] { "a", "a", "c" };
            Assert.That(LoadoutRules.ValidateSubmission(ids, 3, Valid, () => allow, out _), Is.EqualTo(expected));
            Assert.That(LoadoutRules.HasCompleteSelection(ids, Valid, () => allow), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidOptionStopsNormalizationAtTheFailingSlot()
        {
            var ids = new[] { " a ", " bad ", " c " };
            Assert.That(LoadoutRules.ValidateSubmission(ids, 3, Valid, () => false, out var error), Is.False);
            Assert.That(error, Does.Contain("invalid skillId 'bad'"));
            Assert.That(ids, Is.EqualTo(new[] { "a", "bad", " c " }));
        }

        [Test]
        public void WrongSlotCountAndNullSubmissionAreRejectedBeforePolicyCalls()
        {
            bool Unexpected(string _) { Assert.Fail("Invalid-size submissions must not consult option policy"); return false; }
            Assert.That(LoadoutRules.ValidateSubmission(null, 3, Unexpected, () => false, out _), Is.False);
            Assert.That(LoadoutRules.ValidateSubmission(new[] { "a" }, 3, Unexpected, () => false, out _), Is.False);
        }

        [Test]
        public void AutofillPreservesCandidateOrderAndSeparateTrimPolicies()
        {
            var candidates = new List<string>();
            var seen = new HashSet<string>();
            var options = new[] {
                SelectablePropertyOption.Create("skill_loadout", " a ", "A", ""),
                SelectablePropertyOption.Create("skill_loadout", "a", "A", ""),
                SelectablePropertyOption.Create("skill_loadout", "a", "A", "") };
            LoadoutRules.AppendOptionCandidates(candidates, seen, options);
            LoadoutRules.AppendFallbackCandidates(candidates, seen, new[] { " a ", " b ", null, "c", "b" });
            Assert.That(candidates, Is.EqualTo(new[] { " a ", "a", "b", "c" }));
            var used = new HashSet<string> { " a ", "a" };
            Assert.That(LoadoutRules.FindNextAutoFillSkillId(candidates, used, () => false), Is.EqualTo("b"));
            Assert.That(LoadoutRules.FindNextAutoFillSkillId(candidates, used, () => true), Is.EqualTo(" a "));
            used.UnionWith(candidates);
            Assert.That(LoadoutRules.FindNextAutoFillSkillId(candidates, used, () => false), Is.Empty);
        }
    }
}

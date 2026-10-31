using System.Collections.Generic;
using BuddahGo.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Docs/vfx/README.md section 4 "时长同步": every effect a skill plays takes its length from the design tables and
    // every slot effect has the reference length it was tuned at, so a balance change rescales it instead of cutting it.
    public class SkillVfxTimingSyncTests
    {
        const string SkillDatabaseAsset = "Assets/Skills_Assets/Skill Database.asset";
        const string VfxDatabaseAsset = "Assets/Skills_Assets/SkillVfxDatabase.asset";

        [Test]
        public void EverySlotASkillPlaysHasAReferenceLength()
        {
            var vfx = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>(VfxDatabaseAsset);
            Assert.IsNotNull(vfx, VfxDatabaseAsset);
            int slots = 0;
            foreach (KeyValuePair<string, SkillAction.VfxSlotTiming> pair in CollectAtTableValues())
            {
                SkillAction.VfxSlotTiming timing = pair.Value;
                Assert.IsTrue(vfx.TryGetPrefab(timing.vfxId, out GameObject prefab) && prefab != null,
                    pair.Key + " plays '" + timing.vfxId + "', which SkillVfxDatabase does not have");
                var slot = prefab.GetComponent<SkillVfxPhaseController>();
                if (slot == null)
                    continue;
                slots++;
                Assert.Greater(timing.seconds, 0f, pair.Key + " plays '" + timing.vfxId + "' with no length");
                Assert.Greater(slot.ReferenceSeconds(SkillVfxPhase.Imp), 0f,
                    timing.vfxId + " has no Imp reference length: a table change would cut it instead of rescaling it");
            }
            Assert.GreaterOrEqual(slots, 6, "slot effects played by skills");
        }

        [Test]
        public void SlotLengthsFollowTheSkillTable()
        {
            var lengths = new Dictionary<string, float>();
            foreach (KeyValuePair<string, SkillAction.VfxSlotTiming> pair in CollectAtTableValues())
                lengths[pair.Key + "/" + pair.Value.vfxId] = pair.Value.seconds;

            ProjectConfigDatabase db = DesignTableOverlay.Db;
            Assert.IsNotNull(db);
            SkillRecord giant = db.skills.Find(r => r.skillId == "giant");
            SkillRecord reverse = db.skills.Find(r => r.skillId == "reverseturn");
            SkillRecord slow = db.skills.Find(r => r.skillId == "slowtrap");
            Assert.AreEqual(giant.effectDurationSeconds, lengths["giant/giant_release"], 1e-4f);
            Assert.AreEqual(reverse.effectDurationSeconds, lengths["reverseturn/reverse_release"], 1e-4f);
            Assert.AreEqual(reverse.effectDurationSeconds, lengths["reverseturn/reverse_hit"], 1e-4f);
            Assert.AreEqual(slow.effectDurationSeconds, lengths["slowtrap/slow_release"], 1e-4f, "slowtrap's effect must follow the zone length");
        }

        // A skill reports the length it plays: the gameplay length by default; an absolute vfxDurationSeconds on the SO
        // wins and is flagged (the timing window shows "SO 写死了 vfxDurationSeconds，不跟配表"). Empty ids are left out.
        [Test]
        public void VfxDurationOnTheSoOverridesTheGameplayLengthAndIsFlagged()
        {
            var skill = ScriptableObject.CreateInstance<Skill_ReverseTurn>();
            try
            {
                skill.invertDurationSeconds = 3f;
                skill.vfxDurationSeconds = 0f;
                var slots = new List<SkillAction.VfxSlotTiming>();
                skill.CollectVfxSlots(slots);
                Assert.That(slots.Count, Is.EqualTo(2));
                foreach (SkillAction.VfxSlotTiming timing in slots)
                {
                    Assert.That(timing.seconds, Is.EqualTo(3f), timing.vfxId);
                    Assert.That(timing.overridden, Is.False, timing.vfxId);
                }

                skill.vfxDurationSeconds = 5f;
                skill.hitVfxId = " ";
                slots.Clear();
                skill.CollectVfxSlots(slots);
                Assert.That(slots.Count, Is.EqualTo(1));
                Assert.That(slots[0].vfxId, Is.EqualTo(skill.vfxId));
                Assert.That(slots[0].seconds, Is.EqualTo(5f));
                Assert.That(slots[0].overridden, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        // skillId → each effect the skill plays, with the table applied onto copies of the SOs (as in play mode).
        static List<KeyValuePair<string, SkillAction.VfxSlotTiming>> CollectAtTableValues()
        {
            var result = new List<KeyValuePair<string, SkillAction.VfxSlotTiming>>();
            var source = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(source, SkillDatabaseAsset);
            var temp = new List<Object>();
            var scratch = ScriptableObject.CreateInstance<SkillDatabase>();
            temp.Add(scratch);
            try
            {
                var copies = new List<SkillAction>();
                foreach (List<SkillAction> list in new[] { source.normalSkills, source.antiSkills, source.skills })
                {
                    foreach (SkillAction action in list)
                    {
                        if (action == null) continue;
                        SkillAction copy = Object.Instantiate(action);
                        temp.Add(copy);
                        copies.Add(copy);
                        scratch.skills.Add(copy);
                    }
                }
                scratch.ApplyDesignTablesNow();
                var slots = new List<SkillAction.VfxSlotTiming>();
                foreach (SkillAction copy in copies)
                {
                    slots.Clear();
                    copy.CollectVfxSlots(slots);
                    foreach (SkillAction.VfxSlotTiming timing in slots)
                        result.Add(new KeyValuePair<string, SkillAction.VfxSlotTiming>(copy.skillId, timing));
                }
            }
            finally
            {
                foreach (Object o in temp)
                    Object.DestroyImmediate(o);
            }
            return result;
        }
    }
}

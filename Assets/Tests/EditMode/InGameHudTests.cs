using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;

namespace BuddahGo.Tests
{
    // A6 minimal HUD: the pure slot timeline (cooldown, same-skill surcharge window, backfire / freeze lock) and
    // the label formatting the HUD components render.
    public class InGameHudTests
    {
        private const float Window = 5f;
        private const float Multiplier = 1.15f;

        private static SkillSlotHudTimeline ExecutedAt(float now, float cooldown, int stacksPaid)
        {
            var timeline = new SkillSlotHudTimeline();
            timeline.OnSkillUiEvent(now, isOwnSlot: true, isSameSkill: true, cooldown, stacksPaid, Window, Multiplier);
            return timeline;
        }

        [Test]
        public void FreshSlotIsReady()
        {
            SkillSlotHudView view = new SkillSlotHudTimeline().Sample(3f, 0f, 0f);
            Assert.That(view.Kind, Is.EqualTo(SkillSlotHudKind.Ready));
            Assert.That(view.IsCoolingDown, Is.False);
            Assert.That(view.CooldownFill01, Is.EqualTo(0f));
        }

        [Test]
        public void CooldownDrainsFromExecutionWithoutTheRetiredCastLockHold()
        {
            SkillSlotHudTimeline timeline = ExecutedAt(10f, 5f, 0);

            SkillSlotHudView start = timeline.Sample(10f, 0f, 0f);
            Assert.That(start.Kind, Is.EqualTo(SkillSlotHudKind.Cooldown));
            Assert.That(start.SecondsLeft, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(start.CooldownFill01, Is.EqualTo(1f).Within(1e-4f));

            // The old HUD held the fill at 1 for a fallback 2 s cast lock; the fill now drains at once.
            SkillSlotHudView oneSecond = timeline.Sample(11f, 0f, 0f);
            Assert.That(oneSecond.CooldownFill01, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(oneSecond.SecondsLeft, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void SurchargeWindowOpensAtCooldownEndAndClosesAfterTheRepeatWindow()
        {
            SkillSlotHudTimeline timeline = ExecutedAt(10f, 5f, 0);

            SkillSlotHudView open = timeline.Sample(15f, 0f, 0f);
            Assert.That(open.Kind, Is.EqualTo(SkillSlotHudKind.SurchargeWindow));
            Assert.That(open.IsCoolingDown, Is.False);
            Assert.That(open.SecondsLeft, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(open.NextSurchargeStacks, Is.EqualTo(1));
            Assert.That(open.NextGainMultiplier, Is.EqualTo(1.15f).Within(1e-4f));

            Assert.That(timeline.Sample(17.5f, 0f, 0f).SecondsLeft, Is.EqualTo(2.5f).Within(1e-4f));
            // Inclusive end, like SkillRepeatSurcharge.ResolveStacks.
            Assert.That(timeline.Sample(20f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.SurchargeWindow));
            SkillSlotHudView closed = timeline.Sample(20.01f, 0f, 0f);
            Assert.That(closed.Kind, Is.EqualTo(SkillSlotHudKind.Ready));
            Assert.That(closed.NextSurchargeStacks, Is.EqualTo(0));
            Assert.That(closed.NextGainMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void SurchargeCompoundsFromTheStacksTheExecutionPaid()
        {
            SkillSlotHudView view = ExecutedAt(0f, 4f, 2).Sample(4.5f, 0f, 0f);
            Assert.That(view.NextSurchargeStacks, Is.EqualTo(3));
            Assert.That(view.NextGainMultiplier, Is.EqualTo(1.520875f).Within(1e-4f));
        }

        [Test]
        public void BackfireOnAnySlotClearsTheSurchargeWindow()
        {
            SkillSlotHudTimeline timeline = ExecutedAt(0f, 4f, 1);
            // Slot 2 backfired (stacks -1): every skill's stacks are gone on the server.
            timeline.OnSkillUiEvent(2f, isOwnSlot: false, isSameSkill: false, 6f, -1, Window, Multiplier);

            SkillSlotHudView view = timeline.Sample(5f, 0f, 0f);
            Assert.That(view.Kind, Is.EqualTo(SkillSlotHudKind.Ready));
            Assert.That(view.NextSurchargeStacks, Is.EqualTo(0));
        }

        [Test]
        public void BackfireOnOwnSlotSpendsTheCooldownButOpensNoWindow()
        {
            var timeline = new SkillSlotHudTimeline();
            timeline.OnSkillUiEvent(0f, isOwnSlot: true, isSameSkill: true, 4f, -1, Window, Multiplier);

            Assert.That(timeline.Sample(1f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.Cooldown));
            Assert.That(timeline.Sample(4.5f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.Ready));
        }

        [Test]
        public void SameSkillInAnotherSlotMovesTheSurchargeAnchorButNotTheCooldown()
        {
            var timeline = new SkillSlotHudTimeline();
            timeline.OnSkillUiEvent(0f, isOwnSlot: false, isSameSkill: true, 3f, 0, Window, Multiplier);

            Assert.That(timeline.Sample(1f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.Ready));
            SkillSlotHudView window = timeline.Sample(3.5f, 0f, 0f);
            Assert.That(window.Kind, Is.EqualTo(SkillSlotHudKind.SurchargeWindow));
            Assert.That(window.NextSurchargeStacks, Is.EqualTo(1));
        }

        [Test]
        public void OtherSkillsDoNotTouchThisSlot()
        {
            SkillSlotHudTimeline timeline = ExecutedAt(0f, 2f, 0);
            timeline.OnSkillUiEvent(1f, isOwnSlot: false, isSameSkill: false, 9f, 0, Window, Multiplier);
            Assert.That(timeline.Sample(3f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.SurchargeWindow));
        }

        [Test]
        public void FreezeOutranksBackfireWhichOutranksCooldown()
        {
            SkillSlotHudTimeline timeline = ExecutedAt(0f, 8f, 0);

            SkillSlotHudView frozen = timeline.Sample(1f, 7f, 1.2f);
            Assert.That(frozen.Kind, Is.EqualTo(SkillSlotHudKind.Frozen));
            Assert.That(frozen.SecondsLeft, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(frozen.IsLocked, Is.True);
            Assert.That(frozen.IsCoolingDown, Is.True, "the cooldown fill keeps draining under the lock overlay");

            SkillSlotHudView backfire = timeline.Sample(1f, 7f, 0f);
            Assert.That(backfire.Kind, Is.EqualTo(SkillSlotHudKind.BackfireLocked));
            Assert.That(backfire.SecondsLeft, Is.EqualTo(7f).Within(1e-4f));

            Assert.That(new SkillSlotHudTimeline().Sample(0f, 3f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.BackfireLocked),
                "the backfire lock shows on slots that never cast");
        }

        [Test]
        public void ZeroCooldownOpensTheWindowAtOnceAndZeroWindowNeverOpens()
        {
            SkillSlotHudTimeline instant = ExecutedAt(2f, 0f, 0);
            Assert.That(instant.Sample(2f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.SurchargeWindow));

            var noWindow = new SkillSlotHudTimeline();
            noWindow.OnSkillUiEvent(0f, isOwnSlot: true, isSameSkill: true, 1f, 0, 0f, Multiplier);
            Assert.That(noWindow.Sample(1f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.Ready));
        }

        [Test]
        public void ResetForgetsCooldownAndSurcharge()
        {
            SkillSlotHudTimeline timeline = ExecutedAt(0f, 5f, 2);
            timeline.Reset();
            Assert.That(timeline.Sample(1f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.Ready));
            Assert.That(timeline.Sample(6f, 0f, 0f).Kind, Is.EqualTo(SkillSlotHudKind.Ready));
        }

        [Test]
        public void SlotStatusLabels()
        {
            var labels = new SkillSlotHudLabels(backfire: "反噬", frozen: "FROZEN", surcharge: "执念");
            SkillSlotHudTimeline timeline = ExecutedAt(0f, 4f, 1);

            Assert.That(SkillSlotHudFormat.Status(timeline.Sample(0.79f, 0f, 0f), labels), Is.EqualTo("3.3s"),
                "seconds round up to the next tenth so an active state never reads 0.0");
            Assert.That(SkillSlotHudFormat.Status(timeline.Sample(4.8f, 0f, 0f), labels), Is.EqualTo("执念x1.32 4.2s"));
            Assert.That(SkillSlotHudFormat.Status(timeline.Sample(1f, 7.25f, 0f), labels), Is.EqualTo("反噬 7.3s"));
            Assert.That(SkillSlotHudFormat.Status(timeline.Sample(1f, 0f, 1.5f), labels), Is.EqualTo("FROZEN 1.5s"));
            Assert.That(SkillSlotHudFormat.Status(new SkillSlotHudTimeline().Sample(0f, 0f, 0f), labels), Is.Empty);
            Assert.That(SkillSlotHudFormat.Status(timeline.Sample(4.8f, 0f, 0f), new SkillSlotHudLabels("LOCK", "FROZEN", string.Empty)),
                Is.EqualTo("x1.32 4.2s"));
        }

        [Test]
        public void SlotKeyLabels()
        {
            Assert.That(SkillSlotHudFormat.DefaultKeyLabel(0), Is.EqualTo("Q/1"));
            Assert.That(SkillSlotHudFormat.DefaultKeyLabel(1), Is.EqualTo("E/2"));
            Assert.That(SkillSlotHudFormat.DefaultKeyLabel(2), Is.EqualTo("R/3"));
            Assert.That(SkillSlotHudFormat.DefaultKeyLabel(3), Is.Empty);
            Assert.That(SkillSlotHudFormat.KeyLabelFromBindingPaths(new[] { "<Keyboard>/q", "<Keyboard>/1" }), Is.EqualTo("Q/1"));
            Assert.That(SkillSlotHudFormat.KeyLabelFromBindingPaths(new[] { "<Keyboard>/r", " ", null, "<Keyboard>/r", "<Keyboard>/3" }),
                Is.EqualTo("R/3"));
            Assert.That(SkillSlotHudFormat.KeyLabelFromBindingPaths(new string[0]), Is.Empty);
        }

        [Test]
        public void SlotKeyLabelsMatchTheInputActions()
        {
            var actions = new InputSystem_Actions();
            try
            {
                Assert.That(SkillSlotHudFormat.KeyLabelFromBindingPaths(Paths(actions.Player.SkillSlot1)), Is.EqualTo("Q/1"));
                Assert.That(SkillSlotHudFormat.KeyLabelFromBindingPaths(Paths(actions.Player.SkillSlot2)), Is.EqualTo("E/2"));
                Assert.That(SkillSlotHudFormat.KeyLabelFromBindingPaths(Paths(actions.Player.SkillSlot3)), Is.EqualTo("R/3"));
            }
            finally
            {
                // The wrapper's Dispose uses Object.Destroy, which is not allowed in edit mode.
                UnityEngine.Object.DestroyImmediate(actions.asset);
            }
        }

        private static List<string> Paths(UnityEngine.InputSystem.InputAction action)
        {
            var paths = new List<string>();
            foreach (UnityEngine.InputSystem.InputBinding binding in action.bindings)
                paths.Add(binding.effectivePath);
            return paths;
        }

        [Test]
        public void ObsessionLabels()
        {
            Assert.That(ObsessionHudFormat.Probability("反噬", 12.34f), Is.EqualTo("反噬 12%"));
            Assert.That(ObsessionHudFormat.Probability("反噬", 3.456f), Is.EqualTo("反噬 3.5%"));
            Assert.That(ObsessionHudFormat.Probability("反噬", 9.96f), Is.EqualTo("反噬 10%"));
            Assert.That(ObsessionHudFormat.Probability("Backfire", 0f), Is.EqualTo("Backfire 0.0%"));

            Assert.That(ObsessionHudFormat.Status("执念", 512.4f, "反噬中", false, 0f), Is.EqualTo("执念 512"));
            Assert.That(ObsessionHudFormat.Status("执念", 512.4f, "反噬中", true, 7.25f), Is.EqualTo("执念 512  反噬中 7.3s"));

            int key = ObsessionHudFormat.ChangeKey(12.34f, 512.4f, true, 7.25f);
            Assert.That(ObsessionHudFormat.ChangeKey(12.33f, 512.2f, true, 7.21f), Is.EqualTo(key), "same text, same key");
            Assert.That(ObsessionHudFormat.ChangeKey(12.5f, 512.4f, true, 7.25f), Is.Not.EqualTo(key));
            Assert.That(ObsessionHudFormat.ChangeKey(12.34f, 512.4f, true, 7.15f), Is.Not.EqualTo(key));
            Assert.That(ObsessionHudFormat.ChangeKey(12.34f, 512.4f, false, 7.25f), Is.Not.EqualTo(key));
        }

        [Test]
        public void YuanqiLabels()
        {
            Assert.That(HudText.PickCovered("禅足冰封", "frost_lock", _ => true), Is.EqualTo("禅足冰封"));
            Assert.That(HudText.PickCovered("禅足冰封", "frost_lock", _ => false), Is.EqualTo("frost_lock"));
            Assert.That(HudText.PickCovered("  ", "frost_lock", _ => true), Is.EqualTo("frost_lock"));
            Assert.That(HudText.PickCovered("禅足冰封", "frost_lock", null), Is.EqualTo("禅足冰封"));

            Assert.That(YuanqiHudText.Toast("Yuanqi", "frost_lock"), Is.EqualTo("Yuanqi frost_lock"));

            var entries = new List<YuanqiHudEntry>
            {
                new YuanqiHudEntry("frost_lock", "神足通", "禅定缓流"),
                new YuanqiHudEntry("giant_charge", "神足通", "法相金身"),
            };
            Assert.That(YuanqiHudText.LoadoutList("Yuanqi", "none", entries, chosenSkillCount: 1), Is.Empty);
            Assert.That(YuanqiHudText.LoadoutList("Yuanqi", "none", new List<YuanqiHudEntry>(), chosenSkillCount: 3),
                Is.EqualTo("Yuanqi: none"));
            Assert.That(YuanqiHudText.LoadoutList("Yuanqi", "none", entries, chosenSkillCount: 3),
                Is.EqualTo("Yuanqi:\nfrost_lock (神足通 + 禅定缓流)\ngiant_charge (神足通 + 法相金身)"));
        }

        // Glyph coverage of the HUD's default labels. The in-game HUD text uses LiberationSans SDF (static, ASCII)
        // and falls back to the SoloCJK subset (SoloUIBootstrap adds it to TMP_Settings.fallbackFontAssets).
        // A Chinese label the subset does not cover falls back to its ASCII text at runtime (HudGlyphCoverage).
        [Test]
        public void DefaultChineseLabelsTheHudReliesOnAreInTheSoloCjkSubset()
        {
            TMP_FontAsset cjk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SoloCJK/BuddahGoSoloCJK.asset");
            Assert.That(cjk, Is.Not.Null);
            foreach (HudLabel label in new[] { InGameHudLabels.ObsessionValue, InGameHudLabels.BackfireChance,
                         InGameHudLabels.BackfirePenalty, InGameHudLabels.SlotBackfireLock, InGameHudLabels.SlotSurcharge,
                         InGameHudLabels.YuanqiLoadoutNone })
            {
                Assert.That(cjk.HasCharacters(label.Preferred, out uint[] missing, false, false), Is.True,
                    $"'{label.Preferred}' ({label.Key}) misses {Describe(missing)}");
            }
        }

        [Test]
        public void AsciiFallbacksAndFormattingCharactersAreInTheHudFont()
        {
            TMP_FontAsset hud = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Assert.That(hud, Is.Not.Null);
            var texts = new List<string> { "0123456789.%xs/+():QERnone", "Q/1 E/2 R/3" };
            foreach (HudLabel label in InGameHudLabels.All)
                texts.Add(label.Ascii);
            foreach (string text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                Assert.That(hud.HasCharacters(text, out uint[] missing, false, false), Is.True, $"'{text}' misses {Describe(missing)}");
            }
        }

        private static string Describe(uint[] missing)
        {
            if (missing == null) return "nothing";
            var parts = new List<string>();
            foreach (uint c in missing) parts.Add($"U+{c:X4} '{(char)c}'");
            return string.Join(", ", parts);
        }
    }
}

using System.Globalization;
using System.Text;
using NewBuddah.PredictionV2.Core;

namespace BuddahGo.Match
{
    // Remaining seconds of every skill effect a training dummy can observe on its server motor. Launch bypass and
    // steering suppression are motor plumbing, not skill effects, and are deliberately left out.
    internal struct TrainingDummyEffects
    {
        public float FrozenSeconds;
        public float RootSeconds;
        public float AccelSeconds;
        public float AccelExtraMaxSpeed; // < 0 means a slow (slow trap is a negative acceleration).
        public float InvertSeconds;
        public float ScaleSeconds;
        public float ScaleMultiplier;
        public float PushGraceSeconds;

        public bool IsSlow => AccelSeconds > 0f && AccelExtraMaxSpeed < 0f;
        public bool AnyActive => FrozenSeconds > 0f || RootSeconds > 0f || AccelSeconds > 0f || InvertSeconds > 0f
                                 || ScaleSeconds > 0f || PushGraceSeconds > 0f;

        internal static TrainingDummyEffects FromModifiers(in BuddahPredictedModifierState state, uint nowTick, double tickDelta, float frozenSeconds)
        {
            float accel = TrainingDummyReadoutText.RemainingSeconds(state.AccelUntilTick, nowTick, tickDelta);
            float postRootAccel = TrainingDummyReadoutText.RemainingSeconds(state.PostRootAccelUntilTick, nowTick, tickDelta);
            bool direct = accel > 0f;
            return new TrainingDummyEffects
            {
                FrozenSeconds = frozenSeconds > 0f ? frozenSeconds : 0f,
                RootSeconds = TrainingDummyReadoutText.RemainingSeconds(state.RootUntilTick, nowTick, tickDelta),
                AccelSeconds = direct ? accel : postRootAccel,
                AccelExtraMaxSpeed = direct ? state.AccelExtraMaxSpeed : state.PostRootAccelExtraMaxSpeed,
                InvertSeconds = TrainingDummyReadoutText.RemainingSeconds(state.InvertTurnUntilTick, nowTick, tickDelta),
                ScaleSeconds = TrainingDummyReadoutText.RemainingSeconds(state.ScaleUntilTick, nowTick, tickDelta),
                ScaleMultiplier = state.ScaleMultiplier,
                PushGraceSeconds = TrainingDummyReadoutText.RemainingSeconds(state.PushGraceUntilTick, nowTick, tickDelta),
            };
        }
    }

    // Overhead text of a training dummy. ASCII labels only, so the default TMP font renders every glyph.
    internal static class TrainingDummyReadoutText
    {
        internal static float RemainingSeconds(uint untilTick, uint nowTick, double tickDelta)
        {
            if (untilTick <= nowTick || tickDelta <= 0d)
                return 0f;
            return (float)((untilTick - nowTick) * tickDelta);
        }

        // impulseMagnitude < 0 hides the impulse line; resetSeconds < 0 hides the reset countdown.
        internal static string Build(string header, in TrainingDummyEffects fx, float impulseMagnitude, float resetSeconds)
        {
            var text = new StringBuilder(128);
            text.Append(header);
            int lines = 0;
            AppendTimed(text, "FROZEN", fx.FrozenSeconds, ref lines);
            AppendTimed(text, "ROOT", fx.RootSeconds, ref lines);
            AppendTimed(text, fx.IsSlow ? "SLOW" : "ACCEL", fx.AccelSeconds, ref lines);
            AppendTimed(text, "INVERT", fx.InvertSeconds, ref lines);
            if (fx.ScaleSeconds > 0f)
                AppendTimed(text, "SCALE x" + fx.ScaleMultiplier.ToString("0.00", CultureInfo.InvariantCulture), fx.ScaleSeconds, ref lines);
            AppendTimed(text, "PUSH GRACE", fx.PushGraceSeconds, ref lines);
            if (impulseMagnitude >= 0f)
            {
                text.Append("\nIMPULSE ").Append(impulseMagnitude.ToString("0", CultureInfo.InvariantCulture));
                lines++;
            }
            if (resetSeconds >= 0f)
            {
                text.Append("\nRESET ").Append(resetSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append('s');
                lines++;
            }
            if (lines == 0)
                text.Append("\n-");
            return text.ToString();
        }

        private static void AppendTimed(StringBuilder text, string label, float seconds, ref int lines)
        {
            if (seconds <= 0f)
                return;
            text.Append('\n').Append(label).Append(' ').Append(seconds.ToString("0.0", CultureInfo.InvariantCulture)).Append('s');
            lines++;
        }
    }
}

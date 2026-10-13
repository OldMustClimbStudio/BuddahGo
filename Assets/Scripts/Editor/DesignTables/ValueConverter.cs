#if UNITY_EDITOR
using System;
using System.Globalization;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>Cell text → C# values. Mirrors the formats written by Tools/design-tables/build_tables.py.</summary>
    public static class ValueConverter
    {
        public static bool TryBool(string s, out bool value)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true": case "1": case "是": case "yes": value = true; return true;
                case "false": case "0": case "否": case "no": case "": value = false; return true;
                default: value = false; return false;
            }
        }

        public static bool TryVector(string s, int n, out float[] parts)
        {
            string[] split = (s ?? string.Empty).Split(',');
            parts = new float[n];
            if (split.Length != n)
                return false;
            for (int i = 0; i < n; i++)
            {
                if (!float.TryParse(split[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parts[i]))
                    return false;
            }
            return true;
        }

        public static Vector3 ToVector3(string s)
        {
            return TryVector(s, 3, out float[] p) ? new Vector3(p[0], p[1], p[2]) : Vector3.zero;
        }

        public static Vector2 ToVector2(string s)
        {
            return TryVector(s, 2, out float[] p) ? new Vector2(p[0], p[1]) : Vector2.zero;
        }

        public static Color ToColor(string s)
        {
            return TryVector(s, 4, out float[] p) ? new Color(p[0], p[1], p[2], p[3]) : Color.white;
        }

        /// <summary>"time,value,inSlope,outSlope|…" → AnimationCurve. Empty string → empty curve.</summary>
        public static bool TryCurve(string s, out AnimationCurve curve)
        {
            curve = new AnimationCurve();
            if (string.IsNullOrWhiteSpace(s))
                return true;
            string[] keys = s.Split('|');
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryVector(keys[i], 4, out float[] p))
                    return false;
                curve.AddKey(new Keyframe(p[0], p[1], p[2], p[3]));
            }
            return true;
        }

        public static string CurveToString(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return string.Empty;
            string[] parts = new string[curve.length];
            for (int i = 0; i < curve.length; i++)
            {
                Keyframe k = curve[i];
                parts[i] = F(k.time) + "," + F(k.value) + "," + F(k.inTangent) + "," + F(k.outTangent);
            }
            return string.Join("|", parts);
        }

        public static string F(float v)
        {
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static float ToFloat(string s)
        {
            return float.TryParse((s ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        public static int ToInt(string s)
        {
            string t = (s ?? string.Empty).Trim();
            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? (int)Math.Round(f) : 0;
        }
    }
}
#endif

using System;
using UnityEngine;

namespace BuddahGo.Audio
{
    public enum AudioChannel { Music, Sfx }

    /// <summary>
    /// The player's audio settings: master, music and effects volume (0–100 in steps of 10) and whether the game goes
    /// quiet while it is in the background. Saved on this machine (PlayerPrefs) as soon as they change and applied
    /// from game start in every scene. Master drives <see cref="AudioListener.volume"/>; music and effects reach each
    /// AudioSource through <see cref="AudioChannelVolume"/>.
    /// </summary>
    public static class AudioSettingsService
    {
        public const string Master = "master";
        public const string Music = "music";
        public const string Sfx = "sfx";
        public const string Background = "background";
        public const int StepSize = 10;

        private const int DefaultMaster = 80, DefaultMusic = 70, DefaultSfx = 80;
        private const bool DefaultMuteInBackground = true;

        private static int _master = DefaultMaster, _music = DefaultMusic, _sfx = DefaultSfx;
        private static bool _muteInBackground = DefaultMuteInBackground;
        private static bool _loaded;
        private static bool _focused = true;

        /// <summary>Raised after any setting changes (already saved and applied).</summary>
        public static event Action Changed;

        public static bool MuteInBackground { get { Load(); return _muteInBackground; } }

        /// <summary>Volume 0–100 of master, music or sfx.</summary>
        public static int Get(string id)
        {
            Load();
            switch (id)
            {
                case Master: return _master;
                case Music: return _music;
                case Sfx: return _sfx;
                default: return 0;
            }
        }

        /// <summary>Multiplier 0–1 for an AudioSource on this channel (master is applied by the listener).</summary>
        public static float Volume(AudioChannel channel) => Get(channel == AudioChannel.Music ? Music : Sfx) / 100f;

        public static bool IsToggle(string id) => id == Background;

        public static void Set(string id, int value)
        {
            Load();
            value = Mathf.Clamp(Mathf.RoundToInt(value / (float)StepSize) * StepSize, 0, 100);
            switch (id)
            {
                case Master: if (_master == value) return; _master = value; break;
                case Music: if (_music == value) return; _music = value; break;
                case Sfx: if (_sfx == value) return; _sfx = value; break;
                default: return;
            }
            Commit();
        }

        public static void SetMuteInBackground(bool on)
        {
            Load();
            if (_muteInBackground == on) return;
            _muteInBackground = on;
            Commit();
        }

        /// <summary>One step down or up (volumes stop at 0 and 100; the toggle flips).</summary>
        public static void Step(string id, int direction)
        {
            if (IsToggle(id)) SetMuteInBackground(!MuteInBackground);
            else Set(id, Get(id) + direction * StepSize);
        }

        public static void ResetToDefaults()
        {
            Load();
            _master = DefaultMaster; _music = DefaultMusic; _sfx = DefaultSfx; _muteInBackground = DefaultMuteInBackground;
            Commit();
        }

        private static void Commit()
        {
            PlayerPrefs.SetInt("Audio.Master", _master);
            PlayerPrefs.SetInt("Audio.Music", _music);
            PlayerPrefs.SetInt("Audio.Sfx", _sfx);
            PlayerPrefs.SetInt("Audio.MuteInBackground", _muteInBackground ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _master = Mathf.Clamp(PlayerPrefs.GetInt("Audio.Master", DefaultMaster), 0, 100);
            _music = Mathf.Clamp(PlayerPrefs.GetInt("Audio.Music", DefaultMusic), 0, 100);
            _sfx = Mathf.Clamp(PlayerPrefs.GetInt("Audio.Sfx", DefaultSfx), 0, 100);
            _muteInBackground = PlayerPrefs.GetInt("Audio.MuteInBackground", DefaultMuteInBackground ? 1 : 0) != 0;
        }

        private static void Apply()
        {
            AudioListener.volume = _muteInBackground && !_focused ? 0f : _master / 100f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            _loaded = false;
            _focused = true;
            Load();
            Apply();
            var go = new GameObject("[AudioSettings]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FocusWatcher>();
        }

        private sealed class FocusWatcher : MonoBehaviour
        {
            private void OnApplicationFocus(bool focus)
            {
                _focused = focus;
                Apply();
            }
        }
    }
}

using UnityEngine;

namespace BuddahGo.Audio
{
    /// <summary>
    /// Puts an AudioSource on the music or effects channel: its volume is the volume set on the source times the
    /// player's channel volume from <see cref="AudioSettingsService"/>, kept in step when the setting changes.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class AudioChannelVolume : MonoBehaviour
    {
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;

        private AudioSource _source;
        private float _baseVolume = 1f;

        public AudioChannel Channel { get => channel; set { channel = value; Apply(); } }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _baseVolume = _source.volume;
        }

        private void OnEnable()
        {
            AudioSettingsService.Changed += Apply;
            Apply();
        }

        private void OnDisable() => AudioSettingsService.Changed -= Apply;

        private void Apply()
        {
            if (_source != null) _source.volume = _baseVolume * AudioSettingsService.Volume(channel);
        }
    }
}

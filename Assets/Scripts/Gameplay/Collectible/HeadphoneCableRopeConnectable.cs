using Gameplay.Rope;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Collectible
{
    public class HeadphoneCableRopeConnectable : RopeConnectable
    {
        [SerializeField] private bool _muteAudioListener = true;
        [SerializeField] private AudioSource[] _audioSourcesToMute;
        [SerializeField] private UnityEvent _onMuted;

        private bool _hasMuted;

        protected override void OnAllRopesCut(Vector3 hitPoint)
        {
            Mute();
        }

        private void Mute()
        {
            if (_hasMuted) return;

            _hasMuted = true;

            if (_muteAudioListener)
                AudioListener.volume = 0f;

            if (_audioSourcesToMute != null)
            {
                for (int i = 0; i < _audioSourcesToMute.Length; i++)
                {
                    if (_audioSourcesToMute[i] != null)
                        _audioSourcesToMute[i].volume = 0f;
                }
            }

            _onMuted?.Invoke();
        }
    }
}

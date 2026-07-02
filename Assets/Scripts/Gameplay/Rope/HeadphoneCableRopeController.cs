using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Rope
{
    [RequireComponent(typeof(LineRenderer))]
    public class HeadphoneCableRopeController : RopeController
    {
        [SerializeField] private bool _muteAudioListener = true;
        [SerializeField] private AudioSource[] _audioSourcesToAffect;
        [SerializeField, Range(0f, 1f)] private float _targetVolume = 0f;
        [SerializeField] private bool _triggerOnlyWhenAllChainsCut;
        [SerializeField] private UnityEvent _onVolumeChanged;

        private bool _hasTriggered;

        protected override void OnRopeCut(Vector3 hitPoint, int remainingUncutChainCount)
        {
            if (_hasTriggered) return;
            if (_triggerOnlyWhenAllChainsCut && remainingUncutChainCount > 0) return;

            _hasTriggered = true;

            if (_muteAudioListener)
                AudioListener.volume = _targetVolume;

            if (_audioSourcesToAffect != null)
            {
                for (int i = 0; i < _audioSourcesToAffect.Length; i++)
                {
                    if (_audioSourcesToAffect[i] != null)
                        _audioSourcesToAffect[i].volume = _targetVolume;
                }
            }

            _onVolumeChanged?.Invoke();
        }
    }
}

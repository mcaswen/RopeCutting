using Gameplay.Rope;
using Systems;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Collectible
{
    public class NetworkCableRopeConnectable : RopeConnectable
    {
        [SerializeField] private ScreenGlitchEffect _screenGlitchEffect;
        [SerializeField] private float _glitchDuration = 1f;
        [SerializeField, Range(0f, 1f)] private float _glitchStrength = 1f;
        [SerializeField] private bool _findMainCameraEffect = true;
        [SerializeField] private UnityEvent _onGlitchStarted;

        private bool _hasTriggered;

        protected override void OnAllRopesCut(Vector3 hitPoint)
        {
            TriggerGlitch();
        }

        private void TriggerGlitch()
        {
            if (_hasTriggered) return;

            _hasTriggered = true;

            ScreenGlitchEffect effect = ResolveGlitchEffect();
            if (effect != null)
                effect.Play(_glitchDuration, _glitchStrength);

            _onGlitchStarted?.Invoke();
        }

        private ScreenGlitchEffect ResolveGlitchEffect()
        {
            if (_screenGlitchEffect != null)
                return _screenGlitchEffect;

            if (!_findMainCameraEffect || Camera.main == null)
                return null;

            _screenGlitchEffect = Camera.main.GetComponent<ScreenGlitchEffect>();
            return _screenGlitchEffect;
        }
    }
}

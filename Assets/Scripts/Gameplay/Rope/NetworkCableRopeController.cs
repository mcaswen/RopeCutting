using Systems;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Rope
{
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkCableRopeController : RopeController
    {
        [SerializeField] private ScreenGlitchEffect _screenGlitchEffect;
        [SerializeField] private float _glitchDuration = 1f;
        [SerializeField, Range(0f, 1f)] private float _glitchStrength = 1f;
        [SerializeField] private bool _findMainCameraEffect = true;
        [SerializeField] private bool _triggerOnlyWhenAllChainsCut;
        [SerializeField] private UnityEvent _onGlitchStarted;

        private bool _hasTriggered;

        protected override void OnRopeCut(Vector3 hitPoint, int remainingUncutChainCount)
        {
            if (_hasTriggered) return;
            if (_triggerOnlyWhenAllChainsCut && remainingUncutChainCount > 0) return;

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

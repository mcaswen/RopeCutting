using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class ImpactSpriteSwitcher2D : MonoBehaviour
    {
        [Header("Sprite")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite _targetSprite;

        [Header("Shake")]
        [SerializeField] private Transform _shakeTarget;
        [SerializeField, Min(0f)] private float _shakeDuration = 0.18f;
        [SerializeField, Min(0f)] private float _shakeAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float _shakeFrequency = 36f;
        [SerializeField] private Vector2 _shakeAxis = Vector2.right;
        [SerializeField] private bool _restoreLocalPositionAfterShake = true;

        [Header("Hit Filter")]
        [SerializeField] private bool _useLayerFilter;
        [SerializeField] private LayerMask _hitLayers = ~0;
        [SerializeField] private bool _triggerOnce = true;
        [SerializeField] private bool _ignoreWhileAnimating = true;

        [Header("Events")]
        [SerializeField] private UnityEvent _onHit;
        [SerializeField] private UnityEvent _onSpriteChanged;

        private Coroutine _shakeRoutine;
        private Vector3 _initialLocalPosition;
        private bool _hasTriggered;

        private void Awake()
        {
            ResolveReferences();
            _initialLocalPosition = _shakeTarget.localPosition;
        }

        private void OnDisable()
        {
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }

            if (_restoreLocalPositionAfterShake && _shakeTarget != null)
                _shakeTarget.localPosition = _initialLocalPosition;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandleHit(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryHandleHit(collision.collider);
        }

        public void Play()
        {
            if (_triggerOnce && _hasTriggered)
                return;

            _hasTriggered = true;
            _onHit?.Invoke();

            if (_shakeRoutine != null)
                StopCoroutine(_shakeRoutine);

            _shakeRoutine = StartCoroutine(ShakeThenSwitchRoutine());
        }

        public void ResetSwitch()
        {
            _hasTriggered = false;
        }

        private void TryHandleHit(Collider2D other)
        {
            if (other == null)
                return;

            if (_triggerOnce && _hasTriggered)
                return;

            if (_ignoreWhileAnimating && _shakeRoutine != null)
                return;

            if (_useLayerFilter && (_hitLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            Play();
        }

        private IEnumerator ShakeThenSwitchRoutine()
        {
            ResolveReferences();

            if (_shakeTarget == null)
            {
                SwitchSprite();
                yield break;
            }

            Vector3 basePosition = _shakeTarget.localPosition;
            Vector3 axis = _shakeAxis.sqrMagnitude > Mathf.Epsilon
                ? (Vector3)_shakeAxis.normalized
                : Vector3.right;

            if (_shakeDuration <= 0f || _shakeAmplitude <= 0f || _shakeFrequency <= 0f)
            {
                SwitchSprite();
                _shakeRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _shakeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _shakeDuration);
                float damp = 1f - progress;
                float offset = Mathf.Sin(elapsed * _shakeFrequency) * _shakeAmplitude * damp;
                _shakeTarget.localPosition = basePosition + axis * offset;
                yield return null;
            }

            if (_restoreLocalPositionAfterShake)
                _shakeTarget.localPosition = basePosition;

            SwitchSprite();
            _shakeRoutine = null;
        }

        private void SwitchSprite()
        {
            if (_spriteRenderer != null && _targetSprite != null)
                _spriteRenderer.sprite = _targetSprite;

            _onSpriteChanged?.Invoke();
        }

        private void ResolveReferences()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (_shakeTarget == null)
                _shakeTarget = _spriteRenderer != null ? _spriteRenderer.transform : transform;
        }
    }
}

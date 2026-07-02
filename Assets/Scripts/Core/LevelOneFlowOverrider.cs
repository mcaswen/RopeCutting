using System.Collections;
using Gameplay.Interaction;
using Gameplay.Rope;
using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    public class LevelOneFlowOverrider : LevelFlowController
    {
        [Header("Level 1 Override")]
        [SerializeField] private GameObject _lampObject;
        [SerializeField] private GameObject _blackScreen;
        [SerializeField] private InteractiveButton _volumeButton;
        [SerializeField] private float _cancelLampFailureAngle = 90f;
        [SerializeField] private float _rotationTolerance = 1f;
        [SerializeField] private float _blackoutFadeDuration = 1f;
        [SerializeField, Range(0f, 1f)] private float _blackScreenStartAlpha = 0f;
        [SerializeField, Range(0f, 1f)] private float _blackScreenEndAlpha = 1f;
        [SerializeField] private bool _logLampFailureChecks = true;

        private RopeConnectable _lampConnectable;
        private CanvasGroup _blackScreenCanvasGroup;
        private Graphic[] _blackScreenGraphics;
        private SpriteRenderer[] _blackScreenSpriteRenderers;
        private Coroutine _lampFailureRoutine;
        private bool _lampFailureCanceled;
        private bool _failureCausedByLamp;
        private bool _lampFailureSequenceStarted;

        public bool LampFailureCanceled => _lampFailureCanceled;

        protected override void Awake()
        {
            base.Awake();
            ResolveLampConnectable();
            ResolveBlackScreenTargets();

            if (_blackScreen != null)
            {
                SetBlackScreenAlpha(_blackScreenStartAlpha);
                _blackScreen.SetActive(false);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_lampConnectable != null)
                _lampConnectable.AllRopesCut += HandleLampDropped;

            if (_volumeButton != null)
                _volumeButton.OnRotated.AddListener(UpdateLampFailureCancelState);
        }

        protected override void OnDisable()
        {
            if (_lampFailureRoutine != null)
            {
                StopCoroutine(_lampFailureRoutine);
                _lampFailureRoutine = null;
            }

            if (_lampConnectable != null)
                _lampConnectable.AllRopesCut -= HandleLampDropped;

            if (_volumeButton != null)
                _volumeButton.OnRotated.RemoveListener(UpdateLampFailureCancelState);

            base.OnDisable();
        }

        private void Start()
        {
            UpdateLampFailureCancelState();
        }

        protected override bool ShouldFailFromDetector(Collider2D other)
        {
            if (base.ShouldFailFromDetector(other))
                return true;

            if (_lampFailureCanceled)
            {
                LogLampFailureCheck(other, false, "canceled");
                return false;
            }

            if (IsColliderFromObject(other, _lampObject))
            {
                LogLampFailureCheck(other, true, "lamp");
                StartLampFailureSequence();
                return false;
            }

            LogLampFailureCheck(other, false, "not-lamp");
            return false;
        }

        protected override void OnFailure()
        {
            if (_failureCausedByLamp && !_lampFailureSequenceStarted && _blackScreen != null)
            {
                _blackScreen.SetActive(true);
                SetBlackScreenAlpha(_blackScreenEndAlpha);
            }

            base.OnFailure();
        }

        private void UpdateLampFailureCancelState()
        {
            if (_volumeButton != null && _volumeButton.IsAtLocalZAngle(_cancelLampFailureAngle, _rotationTolerance))
                _lampFailureCanceled = true;
        }

        private void HandleLampDropped(Vector3 hitPoint)
        {
            if (!IsPlaying) return;

            UpdateLampFailureCancelState();

            if (_lampFailureCanceled)
            {
                LogLampFailureCheck(null, false, "drop-canceled");
                return;
            }

            LogLampFailureCheck(null, true, "drop");
            StartLampFailureSequence();
        }

        private void StartLampFailureSequence()
        {
            if (_lampFailureSequenceStarted || !IsPlaying) return;

            _failureCausedByLamp = true;
            _lampFailureSequenceStarted = true;
            LockPlayerInput();
            _lampFailureRoutine = StartCoroutine(LampFailureSequence());
        }

        private IEnumerator LampFailureSequence()
        {
            if (_blackScreen != null)
                _blackScreen.SetActive(true);

            float duration = Mathf.Max(0.01f, _blackoutFadeDuration);
            float elapsed = 0f;
            SetBlackScreenAlpha(_blackScreenStartAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetBlackScreenAlpha(Mathf.Lerp(_blackScreenStartAlpha, _blackScreenEndAlpha, t));
                yield return null;
            }

            SetBlackScreenAlpha(_blackScreenEndAlpha);
            _lampFailureRoutine = null;
            CompleteFailure();
        }

        private void ResolveLampConnectable()
        {
            _lampConnectable = null;

            if (_lampObject == null)
                return;

            _lampConnectable = _lampObject.GetComponent<RopeConnectable>();
            if (_lampConnectable == null)
                _lampConnectable = _lampObject.GetComponentInChildren<RopeConnectable>();
            if (_lampConnectable == null)
                _lampConnectable = _lampObject.GetComponentInParent<RopeConnectable>();
        }

        private void ResolveBlackScreenTargets()
        {
            _blackScreenCanvasGroup = null;
            _blackScreenGraphics = null;
            _blackScreenSpriteRenderers = null;

            if (_blackScreen == null)
                return;

            _blackScreenCanvasGroup = _blackScreen.GetComponent<CanvasGroup>();
            _blackScreenGraphics = _blackScreen.GetComponentsInChildren<Graphic>(true);
            _blackScreenSpriteRenderers = _blackScreen.GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void SetBlackScreenAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            if (_blackScreenCanvasGroup != null)
            {
                _blackScreenCanvasGroup.alpha = alpha;
                return;
            }

            if (_blackScreenGraphics != null)
            {
                for (int i = 0; i < _blackScreenGraphics.Length; i++)
                {
                    if (_blackScreenGraphics[i] == null) continue;

                    Color color = _blackScreenGraphics[i].color;
                    color.a = alpha;
                    _blackScreenGraphics[i].color = color;
                }
            }

            if (_blackScreenSpriteRenderers != null)
            {
                for (int i = 0; i < _blackScreenSpriteRenderers.Length; i++)
                {
                    if (_blackScreenSpriteRenderers[i] == null) continue;

                    Color color = _blackScreenSpriteRenderers[i].color;
                    color.a = alpha;
                    _blackScreenSpriteRenderers[i].color = color;
                }
            }
        }

        private void LogLampFailureCheck(Collider2D other, bool willFail, string reason)
        {
            if (!_logLampFailureChecks) return;

            string otherName = other != null ? other.name : "null";
            string lampName = _lampObject != null ? _lampObject.name : "null";
            Debug.Log(
                $"[LevelOneFlowOverrider] detector hit={otherName}, lamp={lampName}, " +
                $"canceled={_lampFailureCanceled}, reason={reason}, willFail={willFail}",
                this);
        }
    }
}

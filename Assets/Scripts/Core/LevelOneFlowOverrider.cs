using System.Collections;
using Gameplay.Interaction;
using Gameplay.Rope;
using Systems;
using Systems.Dialogue;
using UI;
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

        [Header("Level 1 Lamp Dialogue (replaces failure)")]
        [SerializeField] private string _lampCutDialogueId = "level1_light_cut";
        [SerializeField] private RectTransform _lampCutTextPositionAnchor;
        [SerializeField] private Color _lampCutTextColor = Color.white;

        [Header("Level 1 Volume Restore")]
        [SerializeField] private Sprite _volumeButtonRestoredSprite;

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
                _volumeButton.OnRotated.AddListener(HandleVolumeButtonRotated);
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
                _volumeButton.OnRotated.RemoveListener(HandleVolumeButtonRotated);

            base.OnDisable();
        }

        private void Start()
        {
            UpdateLampFailureCancelState(false);
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
            UpdateLampFailureCancelState(false);
        }

        private void HandleVolumeButtonRotated()
        {
            UpdateLampFailureCancelState(true);

            // 旋钮回到0度时：换贴图 + 移除黑屏
            if (_volumeButton != null && _volumeButton.IsAtLocalZAngle(0f, _rotationTolerance))
                RestoreVolumeButton();
        }

        private void RestoreVolumeButton()
        {
            ApplyVolumeButtonRestoredSprite();
            HideBlackScreen();
        }

        private void ApplyVolumeButtonRestoredSprite()
        {
            if (_volumeButtonRestoredSprite == null)
                return;

            SpriteRenderer renderer = _volumeButton.Target.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = _volumeButtonRestoredSprite;
        }

        private void HideBlackScreen()
        {
            if (_blackScreen == null)
                return;

            if (_lampFailureRoutine != null)
            {
                StopCoroutine(_lampFailureRoutine);
                _lampFailureRoutine = null;
            }

            _blackScreen.SetActive(false);
            SetBlackScreenAlpha(_blackScreenStartAlpha);
        }

        private void UpdateLampFailureCancelState(bool playDing)
        {
            bool wasCanceled = _lampFailureCanceled;

            if (_volumeButton != null && _volumeButton.IsAtLocalZAngle(_cancelLampFailureAngle, _rotationTolerance))
                _lampFailureCanceled = true;

            if (playDing && _lampFailureCanceled && !wasCanceled)
                SfxPlayer.Play(SfxId.Ding);
        }

        private void HandleLampDropped(Vector3 hitPoint)
        {
            if (!IsPlaying) return;

            UpdateLampFailureCancelState(false);

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
            SfxPlayer.Play(SfxId.GlassBreak);
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
            PlayLampCutDialogue();
        }

        private void PlayLampCutDialogue()
        {
            if (string.IsNullOrWhiteSpace(_lampCutDialogueId))
            {
                UnlockPlayerInput();
                return;
            }

            // 可能已被 SetPanelActive(false) 失活，需要 include inactive 才能找到
            var subtitleUI = FindObjectOfType<DialogueSubtitleUI>(true);
            if (subtitleUI != null)
            {
                subtitleUI.EnsureActive();
                if (_lampCutTextPositionAnchor != null)
                    subtitleUI.SetSubtitleOverrides(_lampCutTextPositionAnchor, _lampCutTextColor);
                else
                    subtitleUI.SetSubtitleOverrides(Vector2.zero, _lampCutTextColor);
            }

            DialogueManager.Instance?.Play(_lampCutDialogueId, OnLampCutDialogueCompleted);
        }

        private void OnLampCutDialogueCompleted()
        {
            var subtitleUI = FindObjectOfType<DialogueSubtitleUI>(true);
            if (subtitleUI != null)
                subtitleUI.ClearSubtitleOverrides();

            UnlockPlayerInput();
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

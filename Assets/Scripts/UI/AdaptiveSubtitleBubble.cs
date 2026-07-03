using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 根据字幕内容自动调整背景尺寸。挂在字幕预制体上后，可将 DialoguePlayer.OnTextChanged 绑定到 SetText。
    /// </summary>
    [DisallowMultipleComponent]
    public class AdaptiveSubtitleBubble : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _subtitleText;
        [SerializeField] private RectTransform _background;
        [SerializeField] private GameObject _visibilityRoot;

        [Header("Sizing")]
        [SerializeField] private Vector2 _padding = new Vector2(48f, 28f);
        [SerializeField, Min(0f)] private float _minWidth = 160f;
        [SerializeField, Min(0f)] private float _maxWidth = 760f;
        [SerializeField, Min(0f)] private float _minHeight = 56f;
        [SerializeField] private bool _resizeTextRect = true;

        [Header("Behavior")]
        [SerializeField] private bool _hideWhenEmpty = true;
        [SerializeField] private bool _trimIncomingText = true;
        [SerializeField] private bool _refreshEveryFrame;

        private RectTransform _textRect;
        private string _currentText = string.Empty;
        private Tween _tween;
        private Vector3 _originalTextScale = Vector3.one;

        private void Awake()
        {
            ResolveReferences();
            _originalTextScale = _subtitleText != null ? _subtitleText.rectTransform.localScale : Vector3.one;
            _currentText = _subtitleText != null ? _subtitleText.text : string.Empty;
            RefreshSize();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshSize();
            ApplyVisibility();
        }

        private void LateUpdate()
        {
            if (_refreshEveryFrame)
                RefreshSize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            RefreshSize();
        }
#endif

        public void SetText(string text)
        {
            _currentText = _trimIncomingText && text != null ? text.Trim() : text ?? string.Empty;

            if (_subtitleText != null)
                _subtitleText.text = _currentText;

            RefreshSize();
            ApplyVisibility();
        }

        public void Clear()
        {
            SetText(string.Empty);
        }

        public void Show()
        {
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void RefreshSize()
        {
            if (_subtitleText == null || _background == null)
                return;

            if (_textRect == null)
                _textRect = _subtitleText.rectTransform;

            string text = _subtitleText.text ?? string.Empty;
            float maxTextWidth = GetMaxTextWidth();
            float unconstrainedWidth = _subtitleText.GetPreferredValues(text, Mathf.Infinity, 0f).x;
            float textWidth = maxTextWidth > 0f
                ? Mathf.Min(unconstrainedWidth, maxTextWidth)
                : unconstrainedWidth;

            textWidth = Mathf.Max(0f, textWidth);
            Vector2 preferredTextSize = _subtitleText.GetPreferredValues(text, textWidth, 0f);

            float targetWidth = preferredTextSize.x + Mathf.Max(0f, _padding.x);
            float targetHeight = preferredTextSize.y + Mathf.Max(0f, _padding.y);

            targetWidth = Mathf.Max(_minWidth, targetWidth);
            if (_maxWidth > 0f)
                targetWidth = Mathf.Min(_maxWidth, targetWidth);

            targetHeight = Mathf.Max(_minHeight, targetHeight);

            SetRectSize(_background, targetWidth, targetHeight);

            if (_resizeTextRect && _textRect != null)
            {
                float textRectWidth = Mathf.Max(0f, targetWidth - Mathf.Max(0f, _padding.x));
                float textRectHeight = Mathf.Max(0f, targetHeight - Mathf.Max(0f, _padding.y));
                SetRectSize(_textRect, textRectWidth, textRectHeight);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_background);
        }

        private void ResolveReferences()
        {
            if (_subtitleText == null)
                _subtitleText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (_subtitleText != null)
                _textRect = _subtitleText.rectTransform;

            if (_background == null)
                _background = transform as RectTransform;

            if (_visibilityRoot == null)
                _visibilityRoot = gameObject;
        }

        private float GetMaxTextWidth()
        {
            if (_maxWidth <= 0f)
                return 0f;

            return Mathf.Max(0f, _maxWidth - Mathf.Max(0f, _padding.x));
        }

        private void ApplyVisibility()
        {
            if (!_hideWhenEmpty)
            {
                SetVisible(true);
                return;
            }

            SetVisible(!string.IsNullOrWhiteSpace(_currentText));
        }

        private void SetVisible(bool visible)
        {
            if (_visibilityRoot == null || _subtitleText == null) return;

            _tween?.Kill();

            if (visible)
            {
                _subtitleText.rectTransform.localScale = Vector3.zero;
                _visibilityRoot.SetActive(true);
                _tween = _subtitleText.rectTransform.DOScale(_originalTextScale, 0.35f).SetEase(Ease.OutBack);
            }
            else
            {
                _tween = _subtitleText.rectTransform.DOScale(Vector3.zero, 0.2f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => _visibilityRoot.SetActive(false));
            }
        }

        private static void SetRectSize(RectTransform rectTransform, float width, float height)
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}

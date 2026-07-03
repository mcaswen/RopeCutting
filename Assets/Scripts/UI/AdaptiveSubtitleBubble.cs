using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 根据字幕内容自动调整背景尺寸。挂在字幕预制体上后，可将 DialoguePlayer.OnTextChanged 绑定到 SetText。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AdaptiveSubtitleBubble : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshPro _subtitleText;
        [SerializeField] private SpriteRenderer _background;
        [SerializeField] private BoxCollider2D _boxCollider;
        [SerializeField] private GameObject _visibilityRoot;

        [Header("Sizing")]
        [SerializeField] private Vector2 _padding = new Vector2(0.48f, 0.28f);
        [SerializeField] private bool _resizeBoxCollider = true;

        [Header("Behavior")]
        [SerializeField] private bool _hideWhenEmpty = true;
        [SerializeField] private bool _trimIncomingText = true;
        [SerializeField] private bool _refreshEveryFrame;

        private RectTransform _textRect;
        private string _currentText = string.Empty;
        private Tween _tween;
        private Vector3 _originalTextScale = Vector3.one;
        private readonly Vector3[] _textWorldCorners = new Vector3[4];
        private const float LegacyUiUnitScale = 0.01f;
        private const float LegacyUiUnitThreshold = 20f;
        private const float SizeEpsilon = 0.0001f;

        private void Awake()
        {
            NormalizeLegacyPaddingIfNeeded();
            ResolveReferences();
            _originalTextScale = _subtitleText != null ? _subtitleText.rectTransform.localScale : Vector3.one;
            _currentText = _subtitleText != null ? _subtitleText.text : string.Empty;
            RefreshSize();
            ApplyVisibilityInPlayMode();
        }

        private void OnEnable()
        {
            NormalizeLegacyPaddingIfNeeded();
            ResolveReferences();
            RefreshSize();
            ApplyVisibilityInPlayMode();
        }

        private void LateUpdate()
        {
            if (_refreshEveryFrame || !Application.IsPlaying(gameObject))
                RefreshSize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NormalizeLegacyPaddingIfNeeded();
            ResolveReferences();
            RefreshSize();
            ApplyVisibilityInPlayMode();
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

        public void SyncInEditor()
        {
            NormalizeLegacyPaddingIfNeeded();
            ResolveReferences();
            RefreshSize();
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
            NormalizeLegacyPaddingIfNeeded();

            if (_subtitleText == null || _background == null)
                return;

            ResolveTextRect();
            if (_textRect == null)
                return;

            _subtitleText.ForceMeshUpdate(false, true);
            Bounds textFrame = GetTextFrameBounds();

            Vector2 targetCenter = new Vector2(textFrame.center.x, textFrame.center.y);
            Vector2 targetSize = new Vector2(
                textFrame.size.x + Mathf.Max(0f, _padding.x),
                textFrame.size.y + Mathf.Max(0f, _padding.y));

            SetSpriteFrame(targetCenter, targetSize);

            if (_resizeBoxCollider)
                ResizeBoxCollider(targetCenter, targetSize);
        }

        private void ResolveReferences()
        {
            if (_subtitleText == null)
                _subtitleText = GetComponentInChildren<TextMeshPro>(true);

            ResolveTextRect();

            if (_background == null)
                _background = GetComponentInChildren<SpriteRenderer>(true);

            if (_boxCollider == null)
                _boxCollider = GetComponent<BoxCollider2D>();

            if (_visibilityRoot == null)
                _visibilityRoot = gameObject;
        }

        private void NormalizeLegacyPaddingIfNeeded()
        {
            if (_padding.x > LegacyUiUnitThreshold || _padding.y > LegacyUiUnitThreshold)
                _padding *= LegacyUiUnitScale;
        }

        private void ResolveTextRect()
        {
            _textRect = _subtitleText != null ? _subtitleText.rectTransform : null;
        }

        private Bounds GetTextFrameBounds()
        {
            _textRect.GetWorldCorners(_textWorldCorners);

            Vector3 min = transform.InverseTransformPoint(_textWorldCorners[0]);
            Vector3 max = min;

            for (int i = 1; i < _textWorldCorners.Length; i++)
            {
                Vector3 localCorner = transform.InverseTransformPoint(_textWorldCorners[i]);
                min = Vector3.Min(min, localCorner);
                max = Vector3.Max(max, localCorner);
            }

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private void SetSpriteFrame(Vector2 center, Vector2 size)
        {
            if (_background == null)
                return;

            SetBackgroundCenter(center);

            if (_background.drawMode == SpriteDrawMode.Simple || _background.sprite == null)
            {
                Vector2 spriteSize = _background.sprite != null ? _background.sprite.bounds.size : Vector2.one;
                Vector3 localScale = _background.transform.localScale;

                if (spriteSize.x > Mathf.Epsilon)
                    localScale.x = size.x / spriteSize.x;
                if (spriteSize.y > Mathf.Epsilon)
                    localScale.y = size.y / spriteSize.y;

                if (!Approximately(_background.transform.localScale, localScale))
                    _background.transform.localScale = localScale;

                return;
            }

            Vector3 scale = _background.transform.localScale;
            float sizeX = Mathf.Abs(scale.x) > Mathf.Epsilon ? size.x / Mathf.Abs(scale.x) : size.x;
            float sizeY = Mathf.Abs(scale.y) > Mathf.Epsilon ? size.y / Mathf.Abs(scale.y) : size.y;
            Vector2 rendererSize = new Vector2(sizeX, sizeY);

            if (!Approximately(_background.size, rendererSize))
                _background.size = rendererSize;
        }

        private void SetBackgroundCenter(Vector2 center)
        {
            if (_background.transform == transform)
                return;

            Vector3 worldCenter = transform.TransformPoint(new Vector3(center.x, center.y, 0f));
            Transform backgroundParent = _background.transform.parent;
            Vector3 localPosition = backgroundParent != null
                ? backgroundParent.InverseTransformPoint(worldCenter)
                : worldCenter;

            localPosition.z = _background.transform.localPosition.z;

            if (!Approximately(_background.transform.localPosition, localPosition))
                _background.transform.localPosition = localPosition;
        }

        private void ResizeBoxCollider(Vector2 center, Vector2 size)
        {
            if (_boxCollider == null)
                return;

            Vector3 worldSize = transform.TransformVector(new Vector3(size.x, size.y, 0f));
            Vector3 colliderSize = _boxCollider.transform.InverseTransformVector(worldSize);
            Vector2 targetSize = new Vector2(Mathf.Abs(colliderSize.x), Mathf.Abs(colliderSize.y));

            if (!Approximately(_boxCollider.size, targetSize))
                _boxCollider.size = targetSize;

            Vector3 worldCenter = transform.TransformPoint(new Vector3(center.x, center.y, 0f));
            Vector3 colliderCenter = _boxCollider.transform.InverseTransformPoint(worldCenter);
            Vector2 targetOffset = new Vector2(colliderCenter.x, colliderCenter.y);

            if (!Approximately(_boxCollider.offset, targetOffset))
                _boxCollider.offset = targetOffset;
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

        private void ApplyVisibilityInPlayMode()
        {
            if (Application.IsPlaying(gameObject))
                ApplyVisibility();
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

        private static bool Approximately(Vector2 current, Vector2 target)
        {
            return Mathf.Abs(current.x - target.x) <= SizeEpsilon
                && Mathf.Abs(current.y - target.y) <= SizeEpsilon;
        }

        private static bool Approximately(Vector3 current, Vector3 target)
        {
            return Mathf.Abs(current.x - target.x) <= SizeEpsilon
                && Mathf.Abs(current.y - target.y) <= SizeEpsilon
                && Mathf.Abs(current.z - target.z) <= SizeEpsilon;
        }

    }
}

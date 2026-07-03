using Systems.Dialogue;
using TMPro;
using UnityEngine;
using DG.Tweening;

namespace UI
{
    /// <summary>
    /// 挂载到 Canvas 上，自动监听 DialogueManager 显示字幕
    /// </summary>
    public sealed class DialogueSubtitleUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _subtitleText;
        [SerializeField] private float _fadeOutDelay = 0.5f;

        private DialoguePlayer _player;
        private bool _subscribed;
        private Tween _tween;
        private Vector3 _originalTextScale = Vector3.one;

        /// <summary>字幕隐藏时触发（面板 SetActive(false) + 文字清空后）</summary>
        public event System.Action OnHidden;

        // 字幕外观覆写
        private Vector2 _originalAnchoredPosition;
        private Color _originalColor;
        private bool _hasSavedOriginal;

        private void Start()
        {
            var manager = DialogueManager.Instance;
            if (manager == null || manager.Player == null)
                return;

            _player = manager.Player;
            Subscribe();

            if (_subtitleText != null)
                _originalTextScale = _subtitleText.rectTransform.localScale;

            if (_player.IsPlaying)
            {
                if (_player.CurrentLine != null && !_player.CurrentLine.IsWait)
                {
                    if (_subtitleText != null)
                        _subtitleText.text = _player.CurrentLine.Text;
                    SetPanelActive(true);
                }
            }
            else
            {
                SetPanelActive(false);
            }
        }

        private void OnEnable()
        {
            if (_player != null && !_subscribed)
                Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_player == null || _subscribed)
                return;

            _player.SequenceStarted += OnSequenceStarted;
            _player.SequenceCompleted += OnSequenceCompleted;
            _player.LineStarted += OnLineStarted;
            _player.PlaybackStopped += OnPlaybackStopped;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_player == null || !_subscribed)
                return;

            _player.SequenceStarted -= OnSequenceStarted;
            _player.SequenceCompleted -= OnSequenceCompleted;
            _player.LineStarted -= OnLineStarted;
            _player.PlaybackStopped -= OnPlaybackStopped;
            _subscribed = false;
        }

        private void OnSequenceStarted(DialogueSequence sequence)
        {
            SetPanelActive(true);
        }

        private void OnSequenceCompleted(DialogueSequence sequence)
        {
            Invoke(nameof(HidePanel), _fadeOutDelay);
        }

        private void OnPlaybackStopped()
        {
            HidePanel();
        }

        private void OnLineStarted(DialoguePlaybackLine line)
        {
            if (line.IsWait)
                return;

            CancelInvoke(nameof(HidePanel));

            if (_subtitleText != null)
                _subtitleText.text = line.Text;

            SetPanelActive(true);
        }

        private void HidePanel()
        {
            _tween?.Kill();

            if (_subtitleText == null)
            {
                _panel.SetActive(false);
                OnHidden?.Invoke();
                return;
            }

            _tween = _subtitleText.rectTransform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    _panel.SetActive(false);
                    _subtitleText.text = string.Empty;
                    OnHidden?.Invoke();
                });
        }

        /// <summary>
        /// 外部调用：确保字幕面板激活并已订阅事件（用于场景中面板被失活后重新激活）
        /// </summary>
        public void EnsureActive()
        {
            SetPanelActive(true);

            // 如果还没拿到 player 引用，说明 Start 没跑过，尝试再找一次
            if (_player == null)
            {
                var manager = DialogueManager.Instance;
                if (manager != null && manager.Player != null)
                {
                    _player = manager.Player;
                    Subscribe();
                }
            }
        }

        /// <summary>
        /// 外部调用：用 RectTransform 指定字幕文本位置 + 颜色
        /// </summary>
        public void SetSubtitleOverrides(RectTransform positionAnchor, Color color)
        {
            if (_subtitleText == null || positionAnchor == null)
                return;

            if (!_hasSavedOriginal)
            {
                _originalAnchoredPosition = _subtitleText.rectTransform.anchoredPosition;
                _originalColor = _subtitleText.color;
                _hasSavedOriginal = true;
            }

            _subtitleText.rectTransform.anchoredPosition = positionAnchor.anchoredPosition;
            _subtitleText.color = color;
        }

        /// <summary>
        /// 外部调用：临时改变字幕文本的位置和颜色（切灯对话等场景）
        /// </summary>
        public void SetSubtitleOverrides(Vector2 anchoredPosition, Color color)
        {
            if (_subtitleText == null)
                return;

            if (!_hasSavedOriginal)
            {
                _originalAnchoredPosition = _subtitleText.rectTransform.anchoredPosition;
                _originalColor = _subtitleText.color;
                _hasSavedOriginal = true;
            }

            _subtitleText.rectTransform.anchoredPosition = anchoredPosition;
            _subtitleText.color = color;
        }

        /// <summary>
        /// 外部调用：恢复字幕文本原始位置和颜色
        /// </summary>
        public void ClearSubtitleOverrides()
        {
            if (_subtitleText == null || !_hasSavedOriginal)
                return;

            _subtitleText.rectTransform.anchoredPosition = _originalAnchoredPosition;
            _subtitleText.color = _originalColor;
            _hasSavedOriginal = false;
        }

        private void SetPanelActive(bool active)
        {
            if (_panel == null) return;

            _tween?.Kill();

            if (active)
            {
                _panel.SetActive(true);

                if (_subtitleText != null)
                {
                    _subtitleText.rectTransform.localScale = Vector3.zero;
                    _tween = _subtitleText.rectTransform.DOScale(_originalTextScale, 0.35f).SetEase(Ease.OutBack);
                }
            }
            else
            {
                _panel.SetActive(false);
            }
        }
    }
}

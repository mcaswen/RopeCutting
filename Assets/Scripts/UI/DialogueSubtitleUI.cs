using Systems.Dialogue;
using TMPro;
using UnityEngine;

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
            SetPanelActive(false);

            if (_subtitleText != null)
                _subtitleText.text = string.Empty;
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
            if (_panel != null)
                _panel.SetActive(active);
        }
    }
}

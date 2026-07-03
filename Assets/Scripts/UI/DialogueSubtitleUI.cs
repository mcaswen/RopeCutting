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

        private void OnEnable()
        {
            var manager = DialogueManager.Instance;
            if (manager == null || manager.Player == null)
                return;

            _player = manager.Player;
            _player.SequenceStarted += OnSequenceStarted;
            _player.SequenceCompleted += OnSequenceCompleted;
            _player.LineStarted += OnLineStarted;
            _player.PlaybackStopped += OnPlaybackStopped;

            if (!_player.IsPlaying)
                SetPanelActive(false);
        }

        private void OnDisable()
        {
            if (_player == null)
                return;

            _player.SequenceStarted -= OnSequenceStarted;
            _player.SequenceCompleted -= OnSequenceCompleted;
            _player.LineStarted -= OnLineStarted;
            _player.PlaybackStopped -= OnPlaybackStopped;
            _player = null;
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

        private void SetPanelActive(bool active)
        {
            if (_panel != null)
                _panel.SetActive(active);
        }
    }
}

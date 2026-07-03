using System;
using UnityEngine;

namespace Systems.Dialogue
{
    /// <summary>
    /// 场景级对话管理器，提供全局访问点，
    /// 任何脚本都可直接调用 DialogueManager.Instance.Play("sequence_id")
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        [SerializeField] private DialoguePlayer _player;

        private static DialogueManager _instance;
        private Action _onCompleted;

        public static DialogueManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<DialogueManager>();
                return _instance;
            }
        }

        public DialoguePlayer Player => _player;

        private void Awake()
        {
            _instance = this;

            if (_player == null)
                _player = GetComponentInChildren<DialoguePlayer>();

            if (_player != null)
                _player.SequenceCompleted += OnSequenceCompleted;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            if (_player != null)
                _player.SequenceCompleted -= OnSequenceCompleted;
        }

        public void Play(string sequenceId)
        {
            Play(sequenceId, null);
        }

        public void Play(string sequenceId, Action onCompleted)
        {
            if (_player == null)
            {
                Debug.LogWarning("DialogueManager: DialoguePlayer 未赋值");
                return;
            }

            _onCompleted = onCompleted;
            _player.PlaySequence(sequenceId);
        }

        public void Stop()
        {
            _onCompleted = null;
            if (_player != null)
                _player.StopPlayback();
        }

        public bool IsPlaying => _player != null && _player.IsPlaying;

        private void OnSequenceCompleted(DialogueSequence sequence)
        {
            var callback = _onCompleted;
            _onCompleted = null;
            callback?.Invoke();
        }
    }
}

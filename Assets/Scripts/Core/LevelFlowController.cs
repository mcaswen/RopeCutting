using System.Collections;
using Gameplay.Character;
using Gameplay.Collectible;
using Systems;
using Systems.Dialogue;
using UI;
using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    public class LevelFlowController : MonoBehaviour
    {
        public enum LevelState
        {
            Playing,
            Victory,
            Failure
        }

        [SerializeField] private Character _character;
        [SerializeField] private Candy _candy;
        [SerializeField] private ResultPanel _resultPanel;

        [Header("World Space Result (optional)")]
        [SerializeField] private UI.ResultScreen _resultScreen;

        [SerializeField, Min(0f)] private float _victoryResultDelay = 1f;

        [Header("Dialogue (optional)")]
        [SerializeField] private string _introDialogueId;
        [SerializeField] private string _victoryDialogueId;
        [SerializeField] private string _failureDialogueId;
        [SerializeField] private bool _playIntroOnStart = true;

        [Header("Events")]
        [SerializeField] private UnityEvent _onVictory;
        [SerializeField] private UnityEvent _onFailure;

        private LevelState _state = LevelState.Playing;
        private Coroutine _victoryResultRoutine;
        private bool _lockedPlayerInput;

        public LevelState State => _state;
        public bool IsPlaying => _state == LevelState.Playing;
        public bool IsVictory => _state == LevelState.Victory;
        public bool IsFailure => _state == LevelState.Failure;

        protected virtual void Awake()
        {
            if (_resultPanel != null)
                _resultPanel.gameObject.SetActive(false);
        }

        protected virtual void OnEnable()
        {
            if (_character != null)
                _character.OnCandyCollected.AddListener(CompleteVictory);
        }

        protected virtual void Start()
        {
            if (_playIntroOnStart && !string.IsNullOrWhiteSpace(_introDialogueId))
                DialogueManager.Instance?.Play(_introDialogueId);
        }

        protected virtual void OnDisable()
        {
            CancelVictoryResultRoutine();
            UnlockPlayerInput();

            if (_character != null)
                _character.OnCandyCollected.RemoveListener(CompleteVictory);
        }

        public void HandleFailureDetectorEnter(Collider2D other)
        {
            if (!IsPlaying || other == null) return;

            if (ShouldFailFromDetector(other))
                CompleteFailure();
        }

        public void CompleteVictory()
        {
            if (!IsPlaying) return;

            _state = LevelState.Victory;
            LockPlayerInput();
            OnVictory();
        }

        public void CompleteFailure()
        {
            if (!IsPlaying) return;

            _state = LevelState.Failure;
            LockPlayerInput();
            OnFailure();
        }

        protected virtual bool ShouldFailFromDetector(Collider2D other)
        {
            Candy touchedCandy = other.GetComponentInParent<Candy>();
            if (touchedCandy == null)
                return false;

            return _candy == null || touchedCandy == _candy;
        }

        protected virtual void OnVictory()
        {
            if (_victoryResultDelay <= 0f)
            {
                ShowVictoryResult();
                return;
            }

            _victoryResultRoutine = StartCoroutine(ShowVictoryResultAfterDelay());
        }

        private IEnumerator ShowVictoryResultAfterDelay()
        {
            yield return new WaitForSeconds(_victoryResultDelay);
            _victoryResultRoutine = null;
            ShowVictoryResult();
        }

        private void ShowVictoryResult()
        {
            SfxPlayer.Play(SfxId.Win);

            if (!string.IsNullOrWhiteSpace(_victoryDialogueId))
            {
                // 先播胜利对话，播完再显示结算
                DialogueManager.Instance?.Play(_victoryDialogueId, ShowVictoryUI);
                return;
            }

            ShowVictoryUI();
        }

        private void ShowVictoryUI()
        {
            if (_resultScreen != null)
                _resultScreen.ShowVictory();
            else if (_resultPanel != null)
                _resultPanel.ShowVictory();

            _onVictory?.Invoke();
            Debug.Log("Victory! Candy collected!");
        }

        protected virtual void OnFailure()
        {
            CancelVictoryResultRoutine();
            SfxPlayer.Play(SfxId.Lose);

            if (_resultScreen != null)
                _resultScreen.ShowFailure();
            else if (_resultPanel != null)
                _resultPanel.ShowFailure();

            _onFailure?.Invoke();
            Debug.Log("Failure!");
        }

        protected static bool IsColliderFromObject(Collider2D collider, GameObject target)
        {
            if (collider == null || target == null) return false;

            Transform colliderTransform = collider.transform;
            Transform targetTransform = target.transform;
            return colliderTransform == targetTransform || colliderTransform.IsChildOf(targetTransform);
        }

        private void CancelVictoryResultRoutine()
        {
            if (_victoryResultRoutine == null) return;

            StopCoroutine(_victoryResultRoutine);
            _victoryResultRoutine = null;
        }

        protected void LockPlayerInput()
        {
            if (_lockedPlayerInput) return;

            PlayerInputLock.Lock(this);
            _lockedPlayerInput = true;
        }

        protected void UnlockPlayerInput()
        {
            if (!_lockedPlayerInput) return;

            PlayerInputLock.Unlock(this);
            _lockedPlayerInput = false;
        }
    }
}

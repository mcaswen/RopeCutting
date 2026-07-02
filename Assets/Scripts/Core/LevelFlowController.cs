using Gameplay.Character;
using Gameplay.Collectible;
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

        [Header("Events")]
        [SerializeField] private UnityEvent _onVictory;
        [SerializeField] private UnityEvent _onFailure;

        private LevelState _state = LevelState.Playing;

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

        protected virtual void OnDisable()
        {
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
            OnVictory();
        }

        public void CompleteFailure()
        {
            if (!IsPlaying) return;

            _state = LevelState.Failure;
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
            if (_resultPanel != null)
                _resultPanel.ShowVictory();

            _onVictory?.Invoke();
            Debug.Log("Victory! Candy collected!");
        }

        protected virtual void OnFailure()
        {
            if (_resultPanel != null)
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
    }
}

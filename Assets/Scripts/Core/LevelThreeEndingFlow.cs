using System;
using System.Collections;
using Gameplay.Character;
using Gameplay.Rope;
using Systems.Dialogue;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Core
{
    public class LevelThreeEndingFlow : MonoBehaviour
    {
        [Header("Activation")]
        [SerializeField] private bool _activateOnAwake;

        [Header("Ending Screen")]
        [FormerlySerializedAs("_screen1")]
        [SerializeField] private GameObject _endingScreen;
        [FormerlySerializedAs("_screen2")]
        [SerializeField, HideInInspector] private GameObject _legacyEndingScreen2;

        [Header("Ending 1")]
        [SerializeField] private Character _endingCharacter;
        [SerializeField] private string _ending1SequenceId;

        [Header("Ending 2 - Bird")]
        [SerializeField] private RopeConnectable _endingBirdConnectable;
        [SerializeField] private Transform _birdTransform;
        [SerializeField] private Transform _birdTrans1;
        [SerializeField] private Transform _birdTrans2;
        [SerializeField] private Transform _birdTrans3;
        [SerializeField, Min(0.01f)] private float _birdMoveToTrans1Duration = 1f;
        [SerializeField, Min(0.01f)] private float _birdMoveToTrans2Duration = 1f;
        [SerializeField, Min(0.01f)] private float _birdMoveToTrans3Duration = 1f;
        [SerializeField] private AnimationCurve _birdMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool _disableBirdConnectableMotionOnFlight = true;
        [SerializeField] private string _ending2SequenceId;

        [Header("Bird Animation")]
        [SerializeField] private Animator _birdAnimator;
        [SerializeField] private RuntimeAnimatorController _birdFlyController;
        [SerializeField] private string _birdFlyTrigger = "Fly";
        [SerializeField] private string _birdFlyBool = "IsFlying";
        [SerializeField] private string _birdFlyStateName;

        [Header("R Letter")]
        [SerializeField] private Transform _rLetter;
        [SerializeField] private Collider2D _rTrigger;
        [SerializeField, Min(0f)] private float _rContactDistance = 0.5f;
        [FormerlySerializedAs("_dTrans3")]
        [SerializeField] private Transform _rEnd1;
        [SerializeField, Min(0.01f)] private float _rMoveToWaitPointDuration = 1f;
        [SerializeField, Range(0f, 0.99f)] private float _rWaitProgressBeforeDHit = 0.9f;

        [Header("D And EE Letters")]
        [SerializeField] private Transform _dLetter;
        [SerializeField] private Collider2D _dTrigger;
        [SerializeField] private Collider2D _birdCollider;
        [SerializeField, Min(0f)] private float _dContactDistance = 0.5f;
        [SerializeField] private Transform _eeLetters;
        [FormerlySerializedAs("_eeTrans4")]
        [SerializeField] private Transform _eeEnd2;
        [FormerlySerializedAs("_letterRollDuration")]
        [SerializeField, Min(0.01f)] private float _eeMoveDuration = 0.8f;

        [Header("D Drop")]
        [SerializeField] private bool _dropDOnHit = true;
        [SerializeField] private Rigidbody2D _dRigidbody;
        [SerializeField] private Vector2 _dDropInitialVelocity = new Vector2(0f, -2f);
        [SerializeField] private Vector2 _dDropGravity = new Vector2(0f, -9.81f);
        [SerializeField, Min(0f)] private float _dDropDuration = 2f;
        [SerializeField] private float _dRigidbodyGravityScale = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent _onFinalSceneActivated;
        [SerializeField] private UnityEvent _onScreen1Requested;
        [SerializeField] private UnityEvent _onScreen2Requested;
        [SerializeField] private UnityEvent _onRLetterTriggered;
        [SerializeField] private UnityEvent _onDLetterTriggered;

        private bool _isActive;
        private bool _endingCompleted;
        private bool _birdFlightStarted;
        private bool _rLetterTriggered;
        private bool _dLetterTriggered;
        private bool _rFinishRequested;
        private float _rFinishDuration;
        private float _rMoveProgress;
        private Coroutine _birdRoutine;
        private Coroutine _rMoveRoutine;
        private Coroutine _letterRoutine;
        private Coroutine _dDropRoutine;

        public UnityEvent OnFinalSceneActivated => _onFinalSceneActivated;
        public UnityEvent OnScreen1Requested => _onScreen1Requested;
        public UnityEvent OnScreen2Requested => _onScreen2Requested;
        public UnityEvent OnRLetterTriggered => _onRLetterTriggered;
        public UnityEvent OnDLetterTriggered => _onDLetterTriggered;

        private void Awake()
        {
            ResolveReferences();
            SetScreenActive(_endingScreen, false);

            _isActive = _activateOnAwake;
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_endingCharacter != null)
                _endingCharacter.OnCandyCollected.AddListener(HandleEndingCandyCollected);

            if (_endingBirdConnectable != null)
                _endingBirdConnectable.AllRopesCut += HandleEndingBirdAllRopesCut;
        }

        private void OnDisable()
        {
            if (_endingCharacter != null)
                _endingCharacter.OnCandyCollected.RemoveListener(HandleEndingCandyCollected);

            if (_endingBirdConnectable != null)
                _endingBirdConnectable.AllRopesCut -= HandleEndingBirdAllRopesCut;
        }

        public void ActivateFinalScene()
        {
            if (_isActive)
                return;

            _isActive = true;
            _onFinalSceneActivated?.Invoke();
        }

        public void DeactivateFinalScene()
        {
            _isActive = false;
        }

        private void HandleEndingCandyCollected()
        {
            if (!_isActive || _endingCompleted)
                return;

            _endingCompleted = true;
            ShowEndingScreen(_ending1SequenceId);
            _onScreen1Requested?.Invoke();
        }

        private void HandleEndingBirdAllRopesCut(Vector3 hitPoint)
        {
            if (!_isActive || _endingCompleted || _birdFlightStarted)
                return;

            _birdFlightStarted = true;

            if (_disableBirdConnectableMotionOnFlight && _endingBirdConnectable != null)
                _endingBirdConnectable.enabled = false;

            PlayBirdFlyAnimation();

            if (_birdRoutine != null)
                StopCoroutine(_birdRoutine);

            _birdRoutine = StartCoroutine(BirdFlightRoutine());
        }

        private IEnumerator BirdFlightRoutine()
        {
            Transform bird = ResolveBirdTransform();
            if (bird == null)
                yield break;

            if (_birdTrans1 != null)
            {
                yield return MoveBirdTo(
                    bird,
                    _birdTrans1,
                    _birdMoveToTrans1Duration,
                    () => !_rLetterTriggered && IsBirdTouchingLetter(bird, _rLetter, _rTrigger, _rContactDistance),
                    TriggerRLetterBranch,
                    true);
            }

            if (!_rLetterTriggered && _rLetter != null)
                TriggerRLetterBranch();

            if (_birdTrans2 != null)
            {
                yield return MoveBirdTo(
                    bird,
                    _birdTrans2,
                    _birdMoveToTrans2Duration,
                    () => !_dLetterTriggered && IsBirdTouchingLetter(bird, _dLetter, _dTrigger, _dContactDistance),
                    TriggerDLetterBranch,
                    true);
            }

            if (!_dLetterTriggered && (_dLetter != null || _eeLetters != null || _rLetterTriggered))
                TriggerDLetterBranch();

            if (_birdTrans3 != null)
                yield return MoveBirdTo(bird, _birdTrans3, _birdMoveToTrans3Duration, null, null);

            if (_letterRoutine != null)
                yield return _letterRoutine;

            _endingCompleted = true;
            ShowEndingScreen(_ending2SequenceId);
            _onScreen2Requested?.Invoke();
            _birdRoutine = null;
        }

        private IEnumerator MoveBirdTo(
            Transform bird,
            Transform target,
            float duration,
            Func<bool> shouldTrigger,
            Action triggerAction,
            bool stopWhenTriggered = false)
        {
            Vector3 startPosition = bird.position;
            Quaternion startRotation = bird.rotation;
            Vector3 targetPosition = target.position;
            Quaternion targetRotation = target.rotation;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = _birdMoveCurve != null ? _birdMoveCurve.Evaluate(normalized) : normalized;

                bird.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                bird.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);

                if (shouldTrigger != null && shouldTrigger())
                {
                    triggerAction?.Invoke();

                    if (stopWhenTriggered)
                        yield break;
                }

                yield return null;
            }

            bird.SetPositionAndRotation(targetPosition, targetRotation);

            if (shouldTrigger != null && shouldTrigger())
                triggerAction?.Invoke();
        }

        private bool IsBirdTouchingLetter(Transform bird, Transform letter, Collider2D trigger, float contactDistance)
        {
            if (trigger != null)
            {
                if (_birdCollider != null && trigger.IsTouching(_birdCollider))
                    return true;

                Vector2 birdPoint = _birdCollider != null ? (Vector2)_birdCollider.bounds.center : (Vector2)bird.position;
                if (trigger.OverlapPoint(birdPoint))
                    return true;
            }

            if (letter == null || contactDistance <= 0f)
                return false;

            return Vector2.Distance(bird.position, letter.position) <= contactDistance;
        }

        private void TriggerRLetterBranch()
        {
            if (_rLetterTriggered)
                return;

            _rLetterTriggered = true;
            _onRLetterTriggered?.Invoke();

            if (_rLetter == null || _rEnd1 == null)
                return;

            _rFinishRequested = false;
            _rMoveProgress = 0f;

            if (_rMoveRoutine != null)
                StopCoroutine(_rMoveRoutine);

            _rMoveRoutine = StartCoroutine(RLetterMoveRoutine());
        }

        private IEnumerator RLetterMoveRoutine()
        {
            Vector3 startPosition = _rLetter.position;
            Vector3 targetPosition = _rEnd1.position;
            float waitProgress = Mathf.Clamp01(_rWaitProgressBeforeDHit);
            float elapsed = 0f;

            while (!_rFinishRequested)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _rMoveToWaitPointDuration));
                float eased = _birdMoveCurve != null ? _birdMoveCurve.Evaluate(normalized) : normalized;

                _rMoveProgress = Mathf.Min(waitProgress, eased * waitProgress);
                _rLetter.position = Vector3.LerpUnclamped(startPosition, targetPosition, _rMoveProgress);
                yield return null;
            }

            float finishElapsed = 0f;
            float finishStartProgress = _rMoveProgress;
            float finishDuration = Mathf.Max(0.01f, _rFinishDuration);

            while (finishElapsed < finishDuration)
            {
                finishElapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(finishElapsed / finishDuration);
                float eased = _birdMoveCurve != null ? _birdMoveCurve.Evaluate(normalized) : normalized;

                _rMoveProgress = Mathf.LerpUnclamped(finishStartProgress, 1f, eased);
                _rLetter.position = Vector3.LerpUnclamped(startPosition, targetPosition, _rMoveProgress);
                yield return null;
            }

            _rLetter.position = targetPosition;
            _rMoveProgress = 1f;
            _rMoveRoutine = null;
        }

        private void TriggerDLetterBranch()
        {
            if (_dLetterTriggered)
                return;

            _dLetterTriggered = true;
            _onDLetterTriggered?.Invoke();

            DropDLetter();
            RequestRLetterFinish(_eeMoveDuration);

            if (_letterRoutine != null)
                StopCoroutine(_letterRoutine);

            _letterRoutine = StartCoroutine(DAndEeSequenceRoutine());
        }

        private void RequestRLetterFinish(float duration)
        {
            if (!_rLetterTriggered && _rLetter != null)
                TriggerRLetterBranch();

            _rFinishDuration = Mathf.Max(0.01f, duration);
            _rFinishRequested = true;
        }

        private IEnumerator DAndEeSequenceRoutine()
        {
            float duration = Mathf.Max(0.01f, _eeMoveDuration);
            Coroutine eeRoutine = null;

            if (_eeLetters != null && _eeEnd2 != null)
                eeRoutine = StartCoroutine(MoveTransformTo(_eeLetters, _eeEnd2, duration));

            if (eeRoutine != null)
                yield return eeRoutine;
            else
                yield return new WaitForSeconds(duration);

            _letterRoutine = null;
        }

        private void DropDLetter()
        {
            if (!_dropDOnHit)
                return;

            if (_dRigidbody != null)
            {
                _dRigidbody.simulated = true;
                _dRigidbody.bodyType = RigidbodyType2D.Dynamic;
                _dRigidbody.gravityScale = _dRigidbodyGravityScale;
                _dRigidbody.velocity = _dDropInitialVelocity;
                return;
            }

            if (_dLetter == null || _dDropDuration <= 0f)
                return;

            if (_dDropRoutine != null)
                StopCoroutine(_dDropRoutine);

            _dDropRoutine = StartCoroutine(SimulateDLetterDropRoutine());
        }

        private IEnumerator SimulateDLetterDropRoutine()
        {
            float elapsed = 0f;
            Vector2 velocity = _dDropInitialVelocity;

            while (elapsed < _dDropDuration && _dLetter != null)
            {
                float deltaTime = Time.deltaTime;
                elapsed += deltaTime;
                velocity += _dDropGravity * deltaTime;
                _dLetter.position += (Vector3)(velocity * deltaTime);
                yield return null;
            }

            _dDropRoutine = null;
        }

        private IEnumerator MoveTransformTo(Transform movingTransform, Transform target, float duration)
        {
            Vector3 startPosition = movingTransform.position;
            Vector3 targetPosition = target.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = _birdMoveCurve != null ? _birdMoveCurve.Evaluate(normalized) : normalized;

                movingTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            movingTransform.position = targetPosition;
        }

        private void PlayBirdFlyAnimation()
        {
            Animator animator = ResolveBirdAnimator();
            if (animator == null)
                return;

            if (_birdFlyController != null)
                animator.runtimeAnimatorController = _birdFlyController;

            if (!string.IsNullOrEmpty(_birdFlyTrigger) && HasAnimatorParameter(animator, _birdFlyTrigger, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(_birdFlyTrigger);

            if (!string.IsNullOrEmpty(_birdFlyBool) && HasAnimatorParameter(animator, _birdFlyBool, AnimatorControllerParameterType.Bool))
                animator.SetBool(_birdFlyBool, true);

            if (!string.IsNullOrEmpty(_birdFlyStateName))
                animator.Play(_birdFlyStateName, 0, 0f);
            else if (_birdFlyController != null)
                animator.Play(0, 0, 0f);
        }

        private Animator ResolveBirdAnimator()
        {
            if (_birdAnimator != null)
                return _birdAnimator;

            Transform bird = ResolveBirdTransform();
            if (bird == null)
                return null;

            _birdAnimator = bird.GetComponentInChildren<Animator>(true);
            return _birdAnimator;
        }

        private Transform ResolveBirdTransform()
        {
            if (_birdTransform != null)
                return _birdTransform;

            if (_endingBirdConnectable != null)
                _birdTransform = _endingBirdConnectable.transform;

            return _birdTransform;
        }

        private void ResolveReferences()
        {
            if (_endingScreen == null)
                _endingScreen = _legacyEndingScreen2;

            if (_endingBirdConnectable != null)
            {
                if (_endingCharacter == null)
                    _endingCharacter = _endingBirdConnectable.GetComponent<Character>();

                if (_birdTransform == null)
                    _birdTransform = _endingBirdConnectable.transform;

                if (_birdCollider == null)
                    _birdCollider = _endingBirdConnectable.GetComponentInChildren<Collider2D>(true);
            }

            if (_rTrigger == null && _rLetter != null)
                _rTrigger = _rLetter.GetComponent<Collider2D>();

            if (_dTrigger == null && _dLetter != null)
                _dTrigger = _dLetter.GetComponent<Collider2D>();

            if (_dRigidbody == null && _dLetter != null)
                _dRigidbody = _dLetter.GetComponent<Rigidbody2D>();
        }

        private static bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == parameterName)
                    return true;
            }

            return false;
        }

        private static void SetScreenActive(GameObject screen, bool active)
        {
            if (screen != null)
                screen.SetActive(active);
        }

        private void ShowEndingScreen(string sequenceId)
        {
            SetScreenActive(_endingScreen, true);
            PlayEndingScreenSequence(sequenceId);
        }

        private void PlayEndingScreenSequence(string sequenceId)
        {
            if (_endingScreen == null || string.IsNullOrWhiteSpace(sequenceId))
                return;

            RopeSubtitleSequencePlayer sequencePlayer = _endingScreen.GetComponentInChildren<RopeSubtitleSequencePlayer>(true);
            if (sequencePlayer == null)
            {
                Debug.LogWarning($"{nameof(LevelThreeEndingFlow)} could not find {nameof(RopeSubtitleSequencePlayer)} on ending screen '{_endingScreen.name}'.", _endingScreen);
                return;
            }

            sequencePlayer.PlaySequence(sequenceId);
        }
    }
}

using System.Collections;
using Gameplay.Collectible;
using Gameplay.Interaction;
using Gameplay.Rope;
using Systems;
using UnityEngine;

namespace Core
{
    public class LevelThreeFlowOverrider : LevelFlowController
    {
        [Header("Level 3 - Network Recovery")]
        [SerializeField] private NetworkCableRopeController _networkCableRope;
        [SerializeField] private UsbCablePlugInteraction _usbPlug;
        [SerializeField] private FlyingCharacterRopeConnectable _flyingCharacter;
        [SerializeField] private GameObject _lineHeadObject;
        [SerializeField] private GameObject _networkLoadingObject;
        [SerializeField] private bool _hideLineHeadOnStart = true;
        [SerializeField] private bool _hideLoadingOnStart = true;
        [SerializeField] private bool _restartLoadingAnimationOnShow = true;

        [Header("Level 3 - Ending Scene")]
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Transform _endingCameraTarget;
        [SerializeField, Min(0f)] private float _endingCameraMoveDuration = 1.5f;
        [SerializeField] private AnimationCurve _endingCameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private LevelThreeEndingFlow _endingFlow;

        private bool _networkDisconnected;
        private bool _candyCollected;
        private bool _endingTransitionStarted;
        private Coroutine _endingTransitionRoutine;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();

            if (_hideLineHeadOnStart && _lineHeadObject != null)
                _lineHeadObject.SetActive(false);

            if (_hideLoadingOnStart && _networkLoadingObject != null)
                _networkLoadingObject.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_networkCableRope != null)
                _networkCableRope.NetworkCableCut += HandleNetworkCableCut;

            if (_usbPlug != null)
                _usbPlug.Connected += HandleUsbConnected;

            if (_flyingCharacter != null)
                _flyingCharacter.Disappearing += HandleFlyingCharacterDisappearing;
        }

        protected override void OnDisable()
        {
            if (_networkCableRope != null)
                _networkCableRope.NetworkCableCut -= HandleNetworkCableCut;

            if (_usbPlug != null)
                _usbPlug.Connected -= HandleUsbConnected;

            if (_flyingCharacter != null)
                _flyingCharacter.Disappearing -= HandleFlyingCharacterDisappearing;

            if (_endingTransitionRoutine != null)
            {
                StopCoroutine(_endingTransitionRoutine);
                _endingTransitionRoutine = null;
            }

            base.OnDisable();
        }

        protected override void HandleCandyCollected()
        {
            if (!IsPlaying)
                return;

            _candyCollected = true;
        }

        private void HandleFlyingCharacterDisappearing(FlyingCharacterRopeConnectable flyingCharacter)
        {
            if (!IsPlaying)
                return;

            if (!_candyCollected)
            {
                CompleteFailure();
                return;
            }

            StartEndingTransition();
        }

        private void HandleNetworkCableCut(Vector3 hitPoint)
        {
            if (_networkDisconnected)
                return;

            _networkDisconnected = true;

            if (_lineHeadObject != null)
                _lineHeadObject.SetActive(true);

            ShowNetworkLoading();
        }

        private void HandleUsbConnected()
        {
            if (!_networkDisconnected)
                return;

            _networkDisconnected = false;

            if (_networkLoadingObject != null)
                _networkLoadingObject.SetActive(false);
        }

        private void ShowNetworkLoading()
        {
            if (_networkLoadingObject == null)
                return;

            _networkLoadingObject.SetActive(true);

            if (!_restartLoadingAnimationOnShow)
                return;

            SpriteFrameLoopAnimator spriteAnimator = _networkLoadingObject.GetComponent<SpriteFrameLoopAnimator>();
            if (spriteAnimator == null)
                spriteAnimator = _networkLoadingObject.GetComponentInChildren<SpriteFrameLoopAnimator>(true);

            if (spriteAnimator != null)
            {
                spriteAnimator.Play();
                return;
            }

            Animator animator = _networkLoadingObject.GetComponent<Animator>();
            if (animator == null)
                animator = _networkLoadingObject.GetComponentInChildren<Animator>(true);

            if (animator != null)
                animator.Play(0, -1, 0f);
        }

        private void StartEndingTransition()
        {
            if (_endingTransitionStarted)
                return;

            _endingTransitionStarted = true;

            if (_endingTransitionRoutine != null)
                StopCoroutine(_endingTransitionRoutine);

            _endingTransitionRoutine = StartCoroutine(EndingTransitionRoutine());
        }

        private IEnumerator EndingTransitionRoutine()
        {
            LockPlayerInput();

            yield return MoveCameraToEndingTarget();

            UnlockPlayerInput();

            if (_endingFlow != null)
                _endingFlow.ActivateFinalScene();

            _endingTransitionRoutine = null;
        }

        private IEnumerator MoveCameraToEndingTarget()
        {
            if (_endingCameraTarget == null)
                yield break;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera == null)
                yield break;

            Transform cameraTransform = _mainCamera.transform;
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            Vector3 targetPosition = _endingCameraTarget.position;
            Quaternion targetRotation = _endingCameraTarget.rotation;

            if (_endingCameraMoveDuration <= 0f)
            {
                cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _endingCameraMoveDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / _endingCameraMoveDuration);
                float eased = _endingCameraMoveCurve != null ? _endingCameraMoveCurve.Evaluate(normalized) : normalized;

                cameraTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                cameraTransform.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
                yield return null;
            }

            cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private void ResolveReferences()
        {
            if (_networkCableRope == null)
            {
#if UNITY_2022_2_OR_NEWER
                _networkCableRope = FindFirstObjectByType<NetworkCableRopeController>(FindObjectsInactive.Include);
#else
                _networkCableRope = FindObjectOfType<NetworkCableRopeController>(true);
#endif
            }

            if (_usbPlug == null)
            {
#if UNITY_2022_2_OR_NEWER
                _usbPlug = FindFirstObjectByType<UsbCablePlugInteraction>(FindObjectsInactive.Include);
#else
                _usbPlug = FindObjectOfType<UsbCablePlugInteraction>(true);
#endif
            }

            if (_flyingCharacter == null)
            {
#if UNITY_2022_2_OR_NEWER
                _flyingCharacter = FindFirstObjectByType<FlyingCharacterRopeConnectable>(FindObjectsInactive.Include);
#else
                _flyingCharacter = FindObjectOfType<FlyingCharacterRopeConnectable>(true);
#endif
            }

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_endingFlow == null)
            {
#if UNITY_2022_2_OR_NEWER
                _endingFlow = FindFirstObjectByType<LevelThreeEndingFlow>(FindObjectsInactive.Include);
#else
                _endingFlow = FindObjectOfType<LevelThreeEndingFlow>(true);
#endif
            }

            if (_lineHeadObject == null && _usbPlug != null)
                _lineHeadObject = _usbPlug.gameObject;
        }
    }
}

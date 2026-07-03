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

        private bool _networkDisconnected;
        private bool _candyCollected;

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
            if (!IsPlaying || _candyCollected)
                return;

            CompleteFailure();
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

            if (_lineHeadObject == null && _usbPlug != null)
                _lineHeadObject = _usbPlug.gameObject;
        }
    }
}

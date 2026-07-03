using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gameplay.Interaction
{
    [RequireComponent(typeof(Collider2D))]
    public class InteractiveButton : MonoBehaviour
    {
        public enum InteractionMode
        {
            Click,
            Drag,
            Rotate90OnClick,
            RestartCurrentLevelOnClick,
            VolumeSliderOnClick
        }

        private static readonly List<InteractiveButton> Instances = new List<InteractiveButton>();
        private static InteractiveButton _capturedButton;

        [SerializeField] private InteractionMode _mode = InteractionMode.Click;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Transform _target;

        [Header("Drag")]
        [SerializeField] private bool _dragX = true;
        [SerializeField] private bool _dragY = true;
        [SerializeField] private bool _useDragBounds;
        [SerializeField] private Vector2 _dragMin = new Vector2(-5f, -5f);
        [SerializeField] private Vector2 _dragMax = new Vector2(5f, 5f);
        [SerializeField] private bool _moveRigidbody = true;

        [Header("Rotate")]
        [SerializeField] private float _rotationStep = 90f;
        [SerializeField] private float _rotationDuration = 0.15f;

        [Header("Click")]
        [SerializeField] private float _clickMaxScreenDistance = 12f;

        [Header("Volume Slider")]
        [SerializeField] private Sprite _volumeButtonSprite;
        [SerializeField] private RectTransform _volumeSliderRoot;
        [SerializeField] private Slider _volumeSlider;
        [SerializeField, Range(0f, 1f)] private float _defaultVolume = 1f;
        [SerializeField] private bool _hideVolumeSliderOnStart = true;
        [SerializeField] private bool _showVolumeSliderOnClick = true;
        [SerializeField] private bool _setDefaultVolumeWhenOpened;

        [Header("Events")]
        [SerializeField] private UnityEvent _onPressed;
        [SerializeField] private UnityEvent _onClicked;
        [SerializeField] private UnityEvent _onDragStarted;
        [SerializeField] private UnityEvent _onDragEnded;
        [SerializeField] private UnityEvent _onRotated;
        [SerializeField] private UnityEvent _onReleased;
        [SerializeField] private UnityEvent<float> _onVolumeChanged;

        private Collider2D _collider;
        private Rigidbody2D _targetRigidbody;
        private Coroutine _rotationRoutine;
        private Vector2 _pressScreenPosition;
        private Vector3 _dragOffset;
        private bool _isDragging;
        private bool _volumeSliderVisible;

        public static bool IsPointerCaptured => _capturedButton != null;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_target == null)
                _target = transform;

            _targetRigidbody = _target.GetComponent<Rigidbody2D>();

            if (_mode == InteractionMode.VolumeSliderOnClick)
                SetupVolumeSlider();
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
                Instances.Add(this);
        }

        private void OnDisable()
        {
            Instances.Remove(this);

            if (_capturedButton == this)
                ReleasePointer(false);
        }

        private void Update()
        {
            bool canIgnoreInputLock = _mode == InteractionMode.RestartCurrentLevelOnClick;
            if (PlayerInputLock.IsLocked && !canIgnoreInputLock)
            {
                if (_capturedButton == this)
                    ReleasePointer(false);

                return;
            }

            if (_capturedButton != null && _capturedButton != this)
                return;

            if (TryGetPointerDown(out Vector2 screenPosition, out Vector3 worldPosition) && ContainsWorldPoint(worldPosition))
                CapturePointer(screenPosition, worldPosition);

            if (_capturedButton != this)
                return;

            if (_mode == InteractionMode.Drag && TryGetPointerWorldPosition(out _, out Vector3 currentWorldPosition))
                DragTo(currentWorldPosition);

            if (TryGetPointerUp(out Vector2 releaseScreenPosition))
                ReleasePointer(IsClick(releaseScreenPosition));
        }

        private void CapturePointer(Vector2 screenPosition, Vector3 worldPosition)
        {
            _capturedButton = this;
            _pressScreenPosition = screenPosition;
            _dragOffset = _target.position - worldPosition;
            _isDragging = _mode == InteractionMode.Drag;

            _onPressed?.Invoke();

            if (_isDragging)
                _onDragStarted?.Invoke();
        }

        private void ReleasePointer(bool wasClick)
        {
            bool wasDragging = _isDragging;

            _capturedButton = null;
            _isDragging = false;

            if (wasDragging)
                _onDragEnded?.Invoke();

            if (wasClick)
            {
                _onClicked?.Invoke();

                if (_mode == InteractionMode.Rotate90OnClick)
                    RotateByStep();
                else if (_mode == InteractionMode.RestartCurrentLevelOnClick)
                    RestartCurrentLevel();
                else if (_mode == InteractionMode.VolumeSliderOnClick)
                    ToggleVolumeSlider();
            }

            _onReleased?.Invoke();
        }

        private void RestartCurrentLevel()
        {
            PlayerInputLock.Clear();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void DragTo(Vector3 worldPosition)
        {
            Vector3 currentPosition = _target.position;
            Vector3 nextPosition = worldPosition + _dragOffset;
            nextPosition.z = currentPosition.z;

            if (!_dragX)
                nextPosition.x = currentPosition.x;

            if (!_dragY)
                nextPosition.y = currentPosition.y;

            if (_useDragBounds)
            {
                nextPosition.x = Mathf.Clamp(nextPosition.x, _dragMin.x, _dragMax.x);
                nextPosition.y = Mathf.Clamp(nextPosition.y, _dragMin.y, _dragMax.y);
            }

            if (_moveRigidbody && _targetRigidbody != null)
                _targetRigidbody.MovePosition(nextPosition);
            else
                _target.position = nextPosition;
        }

        private void SetupVolumeSlider()
        {
            ApplyVolumeButtonSprite();
            BindVolumeSlider();

            if (_volumeSliderRoot == null || _volumeSlider == null)
                return;

            SetVolumeSliderVisible(!_hideVolumeSliderOnStart);
            SyncVolumeSliderToCurrentVolume();
        }

        private void ApplyVolumeButtonSprite()
        {
            if (_volumeButtonSprite == null)
                return;

            SpriteRenderer targetRenderer = Target.GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
                targetRenderer = Target.GetComponentInChildren<SpriteRenderer>();

            if (targetRenderer != null)
                targetRenderer.sprite = _volumeButtonSprite;
        }

        private void BindVolumeSlider()
        {
            if (_volumeSliderRoot == null && _volumeSlider != null)
                _volumeSliderRoot = _volumeSlider.transform as RectTransform;

            if (_volumeSliderRoot == null)
            {
                Debug.LogWarning($"{name} needs a scene instance of P_VolumeSlider assigned to Volume Slider Root.", this);
                return;
            }

            if (_volumeSlider == null)
                _volumeSlider = _volumeSliderRoot.GetComponent<Slider>();

            if (_volumeSlider == null)
            {
                Debug.LogWarning($"{name} needs a Slider component on the assigned volume slider root.", this);
                return;
            }

            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 1f;
            _volumeSlider.wholeNumbers = false;
            _volumeSlider.onValueChanged.RemoveListener(HandleVolumeSliderValueChanged);
            _volumeSlider.onValueChanged.AddListener(HandleVolumeSliderValueChanged);
        }

        private void ToggleVolumeSlider()
        {
            if (!_showVolumeSliderOnClick)
                return;

            SetVolumeSliderVisible(!_volumeSliderVisible);

            if (!_volumeSliderVisible)
                return;

            if (_setDefaultVolumeWhenOpened)
                SetGameVolume(_defaultVolume);
            else
                SyncVolumeSliderToCurrentVolume();
        }

        private void SetVolumeSliderVisible(bool visible)
        {
            _volumeSliderVisible = visible;

            if (_volumeSliderRoot == null)
                return;

            _volumeSliderRoot.gameObject.SetActive(visible);
        }

        private void SetGameVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            AudioListener.volume = volume;
            SyncVolumeSliderToVolume(volume);

            _onVolumeChanged?.Invoke(volume);
        }

        private void SyncVolumeSliderToCurrentVolume()
        {
            SyncVolumeSliderToVolume(AudioListener.volume);
        }

        private void SyncVolumeSliderToVolume(float volume)
        {
            if (_volumeSlider != null)
                _volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(volume));
        }

        private void HandleVolumeSliderValueChanged(float volume)
        {
            SetGameVolume(volume);
        }

        public static void ApplyExternalGameVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            AudioListener.volume = volume;

            for (int i = 0; i < Instances.Count; i++)
            {
                InteractiveButton button = Instances[i];
                if (button == null || button._mode != InteractionMode.VolumeSliderOnClick)
                    continue;

                button.SyncVolumeSliderToVolume(volume);
            }
        }

        private void RotateByStep()
        {
            if (_rotationRoutine != null)
                StopCoroutine(_rotationRoutine);

            if (_rotationDuration <= 0f)
            {
                _target.localRotation *= Quaternion.Euler(0f, 0f, _rotationStep);
                _onRotated?.Invoke();
                return;
            }

            _rotationRoutine = StartCoroutine(RotateRoutine());
        }

        private IEnumerator RotateRoutine()
        {
            Quaternion startRotation = _target.localRotation;
            Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, _rotationStep);
            float elapsed = 0f;

            while (elapsed < _rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _rotationDuration);
                t = t * t * (3f - 2f * t);
                _target.localRotation = Quaternion.Lerp(startRotation, endRotation, t);
                yield return null;
            }

            _target.localRotation = endRotation;
            _rotationRoutine = null;
            _onRotated?.Invoke();
        }

        private bool IsClick(Vector2 releaseScreenPosition)
        {
            return Vector2.Distance(_pressScreenPosition, releaseScreenPosition) <= _clickMaxScreenDistance;
        }

        private bool ContainsWorldPoint(Vector3 worldPosition)
        {
            return _collider != null && _collider.OverlapPoint(worldPosition);
        }

        private bool TryGetPointerDown(out Vector2 screenPosition, out Vector3 worldPosition)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                    return TryScreenToWorld(touch.position, out screenPosition, out worldPosition);
            }

            if (Input.GetMouseButtonDown(0))
                return TryScreenToWorld(Input.mousePosition, out screenPosition, out worldPosition);

            screenPosition = default;
            worldPosition = default;
            return false;
        }

        private bool TryGetPointerWorldPosition(out Vector2 screenPosition, out Vector3 worldPosition)
        {
            if (Input.touchCount > 0)
                return TryScreenToWorld(Input.GetTouch(0).position, out screenPosition, out worldPosition);

            if (Input.GetMouseButton(0))
                return TryScreenToWorld(Input.mousePosition, out screenPosition, out worldPosition);

            screenPosition = default;
            worldPosition = default;
            return false;
        }

        private bool TryGetPointerUp(out Vector2 screenPosition)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            screenPosition = default;
            return false;
        }

        private bool TryScreenToWorld(Vector2 pointerScreenPosition, out Vector2 screenPosition, out Vector3 worldPosition)
        {
            Camera pointerCamera = _mainCamera != null ? _mainCamera : Camera.main;
            if (pointerCamera == null)
            {
                screenPosition = default;
                worldPosition = default;
                return false;
            }

            Transform target = _target != null ? _target : transform;
            float distanceToTargetPlane = target.position.z - pointerCamera.transform.position.z;
            screenPosition = pointerScreenPosition;
            worldPosition = pointerCamera.ScreenToWorldPoint(new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, distanceToTargetPlane));
            return true;
        }

        public static bool IsPointerOverAny(Vector2 screenPosition, Camera fallbackCamera)
        {
            if (PlayerInputLock.IsLocked)
                return false;

            for (int i = 0; i < Instances.Count; i++)
            {
                InteractiveButton button = Instances[i];
                if (button == null || !button.isActiveAndEnabled)
                    continue;

                Camera pointerCamera = button._mainCamera != null ? button._mainCamera : fallbackCamera;
                if (pointerCamera == null)
                    pointerCamera = Camera.main;

                if (pointerCamera == null)
                    continue;

                Transform target = button._target != null ? button._target : button.transform;
                float distanceToTargetPlane = target.position.z - pointerCamera.transform.position.z;
                Vector3 worldPosition = pointerCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceToTargetPlane));
                if (button.ContainsWorldPoint(worldPosition))
                    return true;
            }

            return false;
        }

        public UnityEvent OnPressed => _onPressed;
        public UnityEvent OnClicked => _onClicked;
        public UnityEvent OnDragStarted => _onDragStarted;
        public UnityEvent OnDragEnded => _onDragEnded;
        public UnityEvent OnRotated => _onRotated;
        public UnityEvent OnReleased => _onReleased;
        public UnityEvent<float> OnVolumeChanged => _onVolumeChanged;

        public Transform Target => _target != null ? _target : transform;
        public float CurrentLocalZAngle => Mathf.Repeat(Target.localEulerAngles.z, 360f);

        public bool IsAtLocalZAngle(float angle, float tolerance = 1f)
        {
            return Mathf.Abs(Mathf.DeltaAngle(CurrentLocalZAngle, angle)) <= tolerance;
        }
    }
}

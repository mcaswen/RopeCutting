using System.Collections;
using System.Collections.Generic;
using Core;
using Systems;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Interaction
{
    [RequireComponent(typeof(Collider2D))]
    public class UsbCablePlugInteraction : MonoBehaviour
    {
        private static readonly List<UsbCablePlugInteraction> Instances = new List<UsbCablePlugInteraction>();
        private static UsbCablePlugInteraction _capturedPlug;

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Transform _plugHead;
        [SerializeField] private Transform _fixedEnd;
        [SerializeField] private Transform _usbPort;
        [SerializeField] private LineRenderer _cableLine;

        [Header("Connect")]
        [SerializeField] private float _connectRadius = 0.6f;
        [SerializeField] private bool _useUsbPortRightAsInsertDirection = true;
        [SerializeField] private Vector2 _insertDirection = Vector2.right;
        [SerializeField] private float _preInsertDistance = 0.45f;
        [SerializeField] private float _alignDuration = 0.08f;
        [SerializeField] private float _insertDuration = 0.18f;
        [SerializeField] private bool _snapRotationToPort = true;

        [Header("Drag")]
        [SerializeField] private bool _moveRigidbody = true;
        [SerializeField] private bool _returnOnMiss;
        [SerializeField] private float _returnDuration = 0.18f;

        [Header("Line")]
        [SerializeField] private float _lineWidth = 0.06f;

        [Header("Events")]
        [SerializeField] private UnityEvent _onDragStarted;
        [SerializeField] private UnityEvent _onReleased;
        [SerializeField] private UnityEvent _onConnected;
        [SerializeField] private UnityEvent _onMissed;

        private Collider2D _collider;
        private Rigidbody2D _plugRigidbody;
        private Coroutine _moveRoutine;
        private Vector3 _dragOffset;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _isDragging;
        private bool _isConnected;

        public static bool IsPointerCaptured => _capturedPlug != null;
        public bool IsConnected => _isConnected;

        private Transform PlugHead => _plugHead != null ? _plugHead : transform;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_plugHead == null)
                _plugHead = transform;

            _plugRigidbody = _plugHead.GetComponent<Rigidbody2D>();
            _initialPosition = _plugHead.position;
            _initialRotation = _plugHead.rotation;
            SetupLineRenderer();
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
                Instances.Add(this);
        }

        private void OnDisable()
        {
            Instances.Remove(this);

            if (_capturedPlug == this)
                _capturedPlug = null;

            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            _isDragging = false;
            PlayerInputLock.Unlock(this);
        }

        private void Update()
        {
            if (!_isDragging && PlayerInputLock.IsLocked)
                return;

            if (_isConnected)
                return;

            if (_capturedPlug != null && _capturedPlug != this)
                return;

            if (TryGetPointerDown(out _, out Vector3 pointerWorldPosition) && ContainsWorldPoint(pointerWorldPosition))
                CapturePointer(pointerWorldPosition);

            if (_capturedPlug != this)
                return;

            if (TryGetPointerWorldPosition(out _, out Vector3 currentWorldPosition))
                DragTo(currentWorldPosition);

            if (TryGetPointerUp())
                ReleasePointer(true);
        }

        private void LateUpdate()
        {
            UpdateCableLine();
        }

        private void CapturePointer(Vector3 worldPosition)
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            _capturedPlug = this;
            _isDragging = true;
            _dragOffset = PlugHead.position - worldPosition;
            PlayerInputLock.Lock(this);
            _onDragStarted?.Invoke();
        }

        private void ReleasePointer(bool invokeReleaseEvent)
        {
            if (_capturedPlug == this)
                _capturedPlug = null;

            bool wasDragging = _isDragging;
            _isDragging = false;

            if (wasDragging && invokeReleaseEvent)
                _onReleased?.Invoke();

            if (!wasDragging)
            {
                PlayerInputLock.Unlock(this);
                return;
            }

            if (IsNearUsbPort())
            {
                StartMoveRoutine(ConnectRoutine());
                return;
            }

            _onMissed?.Invoke();

            if (_returnOnMiss)
            {
                StartMoveRoutine(MoveToRoutine(_initialPosition, _initialRotation, _returnDuration, false));
                return;
            }

            PlayerInputLock.Unlock(this);
        }

        private void DragTo(Vector3 worldPosition)
        {
            Vector3 nextPosition = worldPosition + _dragOffset;
            nextPosition.z = PlugHead.position.z;
            MovePlug(nextPosition);
        }

        private bool IsNearUsbPort()
        {
            if (_usbPort == null)
                return false;

            return Vector2.Distance(PlugHead.position, _usbPort.position) <= _connectRadius;
        }

        private IEnumerator ConnectRoutine()
        {
            Vector3 socketPosition = _usbPort.position;
            Quaternion socketRotation = _snapRotationToPort ? _usbPort.rotation : PlugHead.rotation;
            Vector3 insertDirection = GetInsertDirection();
            Vector3 preInsertPosition = socketPosition - insertDirection * _preInsertDistance;

            yield return MoveToRoutine(preInsertPosition, socketRotation, _alignDuration, true);
            yield return MoveToRoutine(socketPosition, socketRotation, _insertDuration, true);

            _isConnected = true;
            _moveRoutine = null;
            PlayerInputLock.Unlock(this);
            SfxPlayer.Play(SfxId.Click);
            _onConnected?.Invoke();
        }

        private IEnumerator MoveToRoutine(Vector3 targetPosition, Quaternion targetRotation, float duration, bool keepLocked)
        {
            Vector3 startPosition = PlugHead.position;
            Quaternion startRotation = PlugHead.rotation;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);

                MovePlug(Vector3.Lerp(startPosition, targetPosition, t));
                PlugHead.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
                yield return null;
            }

            MovePlug(targetPosition);
            PlugHead.rotation = targetRotation;

            if (!keepLocked)
            {
                _moveRoutine = null;
                PlayerInputLock.Unlock(this);
            }
        }

        private void StartMoveRoutine(IEnumerator routine)
        {
            if (_moveRoutine != null)
                StopCoroutine(_moveRoutine);

            _moveRoutine = StartCoroutine(routine);
        }

        private Vector3 GetInsertDirection()
        {
            Vector3 direction = _useUsbPortRightAsInsertDirection && _usbPort != null
                ? _usbPort.right
                : (Vector3)_insertDirection;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector3.right;

            direction.z = 0f;
            return direction.normalized;
        }

        private void MovePlug(Vector3 position)
        {
            if (_moveRigidbody && _plugRigidbody != null)
                _plugRigidbody.MovePosition(position);
            else
                PlugHead.position = position;
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

        private bool TryGetPointerUp()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }

            return Input.GetMouseButtonUp(0);
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

            Transform plugHead = PlugHead;
            float distanceToPlugPlane = plugHead.position.z - pointerCamera.transform.position.z;
            screenPosition = pointerScreenPosition;
            worldPosition = pointerCamera.ScreenToWorldPoint(new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, distanceToPlugPlane));
            return true;
        }

        private void SetupLineRenderer()
        {
            if (_cableLine == null)
                return;

            _cableLine.positionCount = 2;
            _cableLine.useWorldSpace = true;
            _cableLine.startWidth = _lineWidth;
            _cableLine.endWidth = _lineWidth;
        }

        private void UpdateCableLine()
        {
            if (_cableLine == null || _fixedEnd == null)
                return;

            if (_cableLine.positionCount != 2)
                _cableLine.positionCount = 2;

            _cableLine.SetPosition(0, _fixedEnd.position);
            _cableLine.SetPosition(1, PlugHead.position);
        }

        public static bool IsPointerOverAny(Vector2 screenPosition, Camera fallbackCamera)
        {
            if (PlayerInputLock.IsLocked)
                return false;

            for (int i = 0; i < Instances.Count; i++)
            {
                UsbCablePlugInteraction plug = Instances[i];
                if (plug == null || !plug.isActiveAndEnabled || plug._isConnected)
                    continue;

                Camera pointerCamera = plug._mainCamera != null ? plug._mainCamera : fallbackCamera;
                if (pointerCamera == null)
                    pointerCamera = Camera.main;

                if (pointerCamera == null)
                    continue;

                Transform plugHead = plug.PlugHead;
                float distanceToPlugPlane = plugHead.position.z - pointerCamera.transform.position.z;
                Vector3 worldPosition = pointerCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceToPlugPlane));
                if (plug.ContainsWorldPoint(worldPosition))
                    return true;
            }

            return false;
        }
    }
}

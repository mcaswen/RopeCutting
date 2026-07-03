using System.Collections;
using System.Collections.Generic;
using Core;
using Gameplay.Rope;
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
        [SerializeField] private LineRenderer _ropeLineTemplate;
        [SerializeField] private bool _findRopeLineTemplate = true;
        [SerializeField] private float _lineWidth = 0.06f;

        [Header("Segmented Cable")]
        [SerializeField] private bool _useSegmentedCable = true;
        [SerializeField, Min(0)] private int _cableNodeCount = 12;
        [SerializeField] private float _cableNodeMass = 0.04f;
        [SerializeField] private float _cableNodeRadius = 0.08f;
        [SerializeField] private float _cableNodeGravityScale = 1f;
        [SerializeField] private float _cableNodeDrag = 0.5f;
        [SerializeField] private bool _useCableSpringJoints;
        [SerializeField, Range(0.1f, 2f)] private float _cableSpringRestLengthRatio = 1f;
        [SerializeField] private float _cableSpringFrequency = 5f;
        [SerializeField] private float _cableSpringDampingRatio = 0.3f;

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
        private readonly List<GameObject> _cableNodes = new List<GameObject>();
        private GameObject _cableRuntimeRoot;
        private AnchoredJoint2D _fixedEndJoint;
        private AnchoredJoint2D _plugEndJoint;

        public static bool IsPointerCaptured => _capturedPlug != null;
        public bool IsConnected => _isConnected;
        public event UnityAction Connected;
        public UnityEvent OnConnected => _onConnected;

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

        private void Start()
        {
            CreateSegmentedCable();
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
                Instances.Add(this);

            if (_cableRuntimeRoot != null)
                _cableRuntimeRoot.SetActive(true);
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

            if (_cableRuntimeRoot != null)
                _cableRuntimeRoot.SetActive(false);

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
            Connected?.Invoke();
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
            EnsureCableLine();

            if (_cableLine == null)
                return;

            LineRenderer template = ResolveRopeLineTemplate();
            if (template != null)
                CopyLineRendererSettings(template, _cableLine);

            _cableLine.positionCount = GetCableLinePointCount();
            _cableLine.useWorldSpace = true;
            _cableLine.startWidth = template != null ? template.startWidth : _lineWidth;
            _cableLine.endWidth = template != null ? template.endWidth : _lineWidth;
            _cableLine.textureMode = LineTextureMode.Stretch;
        }

        private void UpdateCableLine()
        {
            if (_cableLine == null)
                SetupLineRenderer();

            if (_cableLine == null || _fixedEnd == null)
                return;

            if (_useSegmentedCable && _cableNodeCount > 0 && _cableNodes.Count == 0)
                CreateSegmentedCable();

            UpdateWorldPlugJoint();

            int pointCount = GetCableLinePointCount();
            if (_cableLine.positionCount != pointCount)
                _cableLine.positionCount = pointCount;

            _cableLine.enabled = true;
            _cableLine.SetPosition(0, _fixedEnd.position);

            if (!_useSegmentedCable || _cableNodes.Count == 0)
            {
                _cableLine.SetPosition(1, PlugHead.position);
                return;
            }

            int writtenPointCount = 1;
            for (int i = 0; i < _cableNodes.Count; i++)
            {
                if (_cableNodes[i] == null)
                    continue;

                _cableLine.SetPosition(writtenPointCount, _cableNodes[i].transform.position);
                writtenPointCount++;
            }

            _cableLine.SetPosition(writtenPointCount, PlugHead.position);

            if (_cableLine.positionCount != writtenPointCount + 1)
                _cableLine.positionCount = writtenPointCount + 1;
        }

        private int GetCableLinePointCount()
        {
            return _useSegmentedCable && _cableNodeCount > 0
                ? _cableNodeCount + 2
                : 2;
        }

        private void CreateSegmentedCable()
        {
            DestroySegmentedCable();

            if (!_useSegmentedCable || _cableNodeCount <= 0 || _fixedEnd == null || PlugHead == null)
                return;

            EnsureCableLine();
            SetupLineRenderer();

            _cableRuntimeRoot = new GameObject(gameObject.name + "_CablePhysics");
            _cableRuntimeRoot.transform.position = Vector3.zero;

            Rigidbody2D fixedEndRigidbody = EnsureKinematicBody(_fixedEnd);
            Rigidbody2D plugRigidbody = _plugRigidbody;

            Vector2 startPosition = _fixedEnd.position;
            Vector2 endPosition = PlugHead.position;
            float segmentDistance = Vector2.Distance(startPosition, endPosition) / (_cableNodeCount + 1);

            Collider2D[] ignoredColliders = GetComponentsInChildren<Collider2D>();
            CircleCollider2D previousNodeCollider = null;

            for (int i = 0; i < _cableNodeCount; i++)
            {
                float t = (i + 1) / (float)(_cableNodeCount + 1);
                Vector2 nodePosition = Vector2.Lerp(startPosition, endPosition, t);

                GameObject node = new GameObject("UsbCableNode_" + i);
                node.transform.position = nodePosition;
                node.transform.SetParent(_cableRuntimeRoot.transform, true);

                Rigidbody2D nodeRigidbody = node.AddComponent<Rigidbody2D>();
                nodeRigidbody.mass = Mathf.Max(0.001f, _cableNodeMass);
                nodeRigidbody.drag = _cableNodeDrag;
                nodeRigidbody.gravityScale = _cableNodeGravityScale;

                CircleCollider2D nodeCollider = node.AddComponent<CircleCollider2D>();
                nodeCollider.radius = _cableNodeRadius;

                IgnoreCableNodeCollisions(nodeCollider, ignoredColliders);
                if (previousNodeCollider != null)
                    Physics2D.IgnoreCollision(nodeCollider, previousNodeCollider);

                previousNodeCollider = nodeCollider;
                _cableNodes.Add(node);
            }

            Rigidbody2D firstNodeRigidbody = _cableNodes[0].GetComponent<Rigidbody2D>();
            _fixedEndJoint = CreateCableJoint(_fixedEnd.gameObject, firstNodeRigidbody, segmentDistance);

            for (int i = 0; i < _cableNodes.Count; i++)
            {
                GameObject node = _cableNodes[i];
                AnchoredJoint2D joint = CreateCableJoint(node);

                if (i < _cableNodes.Count - 1)
                {
                    joint.connectedBody = _cableNodes[i + 1].GetComponent<Rigidbody2D>();
                }
                else
                {
                    joint.connectedBody = plugRigidbody;
                    joint.connectedAnchor = plugRigidbody != null ? Vector2.zero : (Vector2)PlugHead.position;
                    _plugEndJoint = joint;
                }

                ConfigureCableJoint(joint, segmentDistance);
            }
        }

        private Rigidbody2D EnsureKinematicBody(Transform target)
        {
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body == null)
                body = target.gameObject.AddComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Kinematic;
            return body;
        }

        private AnchoredJoint2D CreateCableJoint(GameObject owner, Rigidbody2D connectedBody, float distance)
        {
            AnchoredJoint2D joint = CreateCableJoint(owner);
            joint.connectedBody = connectedBody;
            ConfigureCableJoint(joint, distance);
            return joint;
        }

        private AnchoredJoint2D CreateCableJoint(GameObject owner)
        {
            if (_useCableSpringJoints)
            {
                SpringJoint2D springJoint = owner.AddComponent<SpringJoint2D>();
                springJoint.autoConfigureDistance = false;
                springJoint.frequency = _cableSpringFrequency;
                springJoint.dampingRatio = _cableSpringDampingRatio;
                return springJoint;
            }

            DistanceJoint2D distanceJoint = owner.AddComponent<DistanceJoint2D>();
            distanceJoint.autoConfigureDistance = false;
            return distanceJoint;
        }

        private void ConfigureCableJoint(AnchoredJoint2D joint, float distance)
        {
            joint.autoConfigureConnectedAnchor = false;

            if (joint is DistanceJoint2D distanceJoint)
            {
                distanceJoint.autoConfigureDistance = false;
                distanceJoint.distance = distance;
            }
            else if (joint is SpringJoint2D springJoint)
            {
                springJoint.autoConfigureDistance = false;
                springJoint.distance = distance * _cableSpringRestLengthRatio;
                springJoint.frequency = _cableSpringFrequency;
                springJoint.dampingRatio = _cableSpringDampingRatio;
            }
        }

        private void UpdateWorldPlugJoint()
        {
            if (_plugEndJoint == null || _plugEndJoint.connectedBody != null)
                return;

            _plugEndJoint.connectedAnchor = PlugHead.position;
        }

        private void IgnoreCableNodeCollisions(Collider2D nodeCollider, Collider2D[] ignoredColliders)
        {
            if (nodeCollider == null || ignoredColliders == null)
                return;

            for (int i = 0; i < ignoredColliders.Length; i++)
            {
                if (ignoredColliders[i] != null && ignoredColliders[i] != nodeCollider)
                    Physics2D.IgnoreCollision(nodeCollider, ignoredColliders[i]);
            }
        }

        private void DestroySegmentedCable()
        {
            if (_fixedEndJoint != null)
            {
                Destroy(_fixedEndJoint);
                _fixedEndJoint = null;
            }

            _plugEndJoint = null;

            for (int i = _cableNodes.Count - 1; i >= 0; i--)
            {
                if (_cableNodes[i] != null)
                    Destroy(_cableNodes[i]);
            }

            _cableNodes.Clear();

            if (_cableRuntimeRoot != null)
            {
                Destroy(_cableRuntimeRoot);
                _cableRuntimeRoot = null;
            }
        }

        private void EnsureCableLine()
        {
            if (_cableLine != null)
                return;

            LineRenderer existingLine = GetComponentInChildren<LineRenderer>();
            if (existingLine != null)
            {
                _cableLine = existingLine;
                return;
            }

            GameObject lineObject = new GameObject("UsbCableLine");
            lineObject.transform.SetParent(transform, false);
            _cableLine = lineObject.AddComponent<LineRenderer>();
        }

        private LineRenderer ResolveRopeLineTemplate()
        {
            if (_ropeLineTemplate != null)
                return _ropeLineTemplate;

            if (!_findRopeLineTemplate)
                return null;

#if UNITY_2022_2_OR_NEWER
            RopeController[] ropes = FindObjectsByType<RopeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            RopeController[] ropes = FindObjectsOfType<RopeController>(true);
#endif

            for (int i = 0; i < ropes.Length; i++)
            {
                if (ropes[i] == null)
                    continue;

                LineRenderer lineRenderer = ropes[i].GetComponent<LineRenderer>();
                if (lineRenderer != null && lineRenderer != _cableLine)
                {
                    _ropeLineTemplate = lineRenderer;
                    return _ropeLineTemplate;
                }
            }

            return null;
        }

        private static void CopyLineRendererSettings(LineRenderer source, LineRenderer target)
        {
            target.sharedMaterial = source.sharedMaterial;
            target.colorGradient = source.colorGradient;
            target.alignment = source.alignment;
            target.numCapVertices = source.numCapVertices;
            target.numCornerVertices = source.numCornerVertices;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.textureMode = source.textureMode;
            target.generateLightingData = source.generateLightingData;
            target.shadowBias = source.shadowBias;
        }

        private void OnDestroy()
        {
            DestroySegmentedCable();
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

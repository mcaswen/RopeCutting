using UnityEngine;
using Core;
using Gameplay.Interaction;
using Gameplay.Rope;

namespace Gameplay.Cutting
{
    /// <summary>
    /// 绳子切割器，检测鼠标/触屏拖拽划线并判断是否与绳子相交
    /// </summary>
    public class RopeCutter : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;

        private RopeController[] _ropes;

        private Vector2 _lastMousePosition;
        private bool _isDragging;
        private bool _isBlockedByInteractive;

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            FindRopesInScene();
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (PlayerInputLock.IsLocked)
            {
                _isDragging = false;
                _isBlockedByInteractive = false;
                return;
            }

            if (InteractiveButton.IsPointerCaptured)
            {
                _isDragging = false;
                return;
            }

            // 鼠标输入（编辑器测试）
            if (Input.GetMouseButtonDown(0))
            {
                if (InteractiveButton.IsPointerOverAny(Input.mousePosition, _mainCamera))
                {
                    _isBlockedByInteractive = true;
                    _isDragging = false;
                    return;
                }

                _isBlockedByInteractive = false;
                _isDragging = true;
                _lastMousePosition = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
                _isBlockedByInteractive = false;
            }

            if (!_isBlockedByInteractive && _isDragging && Input.GetMouseButton(0))
            {
                Vector2 currentMousePosition = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                CheckRopeCut(_lastMousePosition, currentMousePosition);
                _lastMousePosition = currentMousePosition;
            }

            // 触屏输入（移动设备）
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Vector2 touchWorldPos = _mainCamera.ScreenToWorldPoint(touch.position);

                if (touch.phase == TouchPhase.Began)
                {
                    if (InteractiveButton.IsPointerOverAny(touch.position, _mainCamera))
                    {
                        _isBlockedByInteractive = true;
                        _isDragging = false;
                        return;
                    }

                    _isBlockedByInteractive = false;
                    _lastMousePosition = touchWorldPos;
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (!_isBlockedByInteractive)
                    {
                        CheckRopeCut(_lastMousePosition, touchWorldPos);
                        _lastMousePosition = touchWorldPos;
                    }
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _isBlockedByInteractive = false;
                }
            }
        }

        /// <summary>
        /// 检测划线线段是否与绳子相交
        /// </summary>
        private void CheckRopeCut(Vector2 lineStart, Vector2 lineEnd)
        {
            if (_ropes == null || _ropes.Length == 0)
                FindRopesInScene();

            foreach (RopeController rope in _ropes)
            {
                if (rope == null || rope.IsCut) continue;

                if (rope.TryCut(lineStart, lineEnd))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 运行时自动查找场景中的绳子
        /// </summary>
        public void FindRopesInScene()
        {
#if UNITY_2022_2_OR_NEWER
            _ropes = FindObjectsByType<RopeController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            _ropes = FindObjectsOfType<RopeController>();
#endif
        }
    }
}

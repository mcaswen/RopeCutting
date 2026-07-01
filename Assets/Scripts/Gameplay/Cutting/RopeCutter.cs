using UnityEngine;
using Gameplay.Rope;

namespace Gameplay.Cutting
{
    /// <summary>
    /// 绳子切割器，检测鼠标/触屏拖拽划线并判断是否与绳子相交
    /// </summary>
    public class RopeCutter : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private RopeController[] _ropes;

        private Vector2 _lastMousePosition;
        private bool _isDragging;

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // 鼠标输入（编辑器测试）
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _lastMousePosition = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            if (_isDragging && Input.GetMouseButton(0))
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
                    _lastMousePosition = touchWorldPos;
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    CheckRopeCut(_lastMousePosition, touchWorldPos);
                    _lastMousePosition = touchWorldPos;
                }
            }
        }

        /// <summary>
        /// 检测划线线段是否与绳子相交
        /// </summary>
        private void CheckRopeCut(Vector2 lineStart, Vector2 lineEnd)
        {
            foreach (RopeController rope in _ropes)
            {
                if (rope == null || rope.IsCut) continue;

                Vector3[] ropePositions = rope.GetRopePositions();
                for (int i = 0; i < ropePositions.Length - 1; i++)
                {
                    Vector2 ropeSegStart = ropePositions[i];
                    Vector2 ropeSegEnd = ropePositions[i + 1];

                    if (SegmentsIntersect(lineStart, lineEnd, ropeSegStart, ropeSegEnd))
                    {
                        Vector2 hitPoint = GetIntersectionPoint(lineStart, lineEnd, ropeSegStart, ropeSegEnd);
                        rope.Cut(hitPoint, i);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 计算两条线段的交点坐标
        /// </summary>
        private Vector2 GetIntersectionPoint(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float denominator = (b2.y - b1.y) * (a2.x - a1.x) - (b2.x - b1.x) * (a2.y - a1.y);
            if (Mathf.Approximately(denominator, 0))
                return (a1 + a2) / 2;

            float ua = ((b2.x - b1.x) * (a1.y - b1.y) - (b2.y - b1.y) * (a1.x - b1.x)) / denominator;
            return new Vector2(a1.x + ua * (a2.x - a1.x), a1.y + ua * (a2.y - a1.y));
        }

        /// <summary>
        /// 判断两条线段是否相交（使用向量叉积方向法）
        /// </summary>
        private bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float o1 = Orientation(a1, a2, b1);
            float o2 = Orientation(a1, a2, b2);
            float o3 = Orientation(b1, b2, a1);
            float o4 = Orientation(b1, b2, a2);

            // 一般情况：相互跨立
            if (o1 * o2 < 0 && o3 * o4 < 0)
                return true;

            // 处理共线情况（忽略，绳子切割不需要太精确）
            return false;
        }

        /// <summary>
        /// 计算三点叉积方向，用于判断线段相交
        /// </summary>
        private float Orientation(Vector2 p, Vector2 q, Vector2 r)
        {
            return (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
        }

        /// <summary>
        /// 运行时自动查找场景中的绳子
        /// </summary>
        public void FindRopesInScene()
        {
            _ropes = FindObjectsOfType<RopeController>();
        }
    }
}

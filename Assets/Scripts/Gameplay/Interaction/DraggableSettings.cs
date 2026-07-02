using System.Collections;
using UnityEngine;

namespace Gameplay.Interaction
{
    /// <summary>
    /// Setting 物体拖拽：拖到触发器区域内 → 生成一根棍子连接到 Anchor5；
    /// 拖到区域外 → 平滑归位。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DraggableSettings : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Collider2D _validZone;
        [SerializeField] private Transform _anchor5;
        [SerializeField] private Transform _stickParent;

        [Header("Snap Back")]
        [SerializeField] private float _snapBackDuration = 0.3f;

        [Header("Stick")]
        [SerializeField] private float _stickThickness = 0.3f;
        [SerializeField] private Sprite _stickSprite;

        /// <summary>
        /// 标记是否有任意 DraggableSettings 正在被拖拽，供 RopeCutter 检查以禁用切割
        /// </summary>
        public static bool IsDraggingAny { get; private set; }

        /// <summary>
        /// 检测当前鼠标位置是否在任意 DraggableSettings 的碰撞体上
        /// </summary>
        public static bool IsPointerOverAny(Vector2 screenPos, Camera cam)
        {
            if (cam == null) return false;
            Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);
            Collider2D hit = Physics2D.OverlapPoint(worldPos);
            return hit != null && hit.GetComponentInParent<DraggableSettings>() != null;
        }

        private Vector3 _originalLocalPosition;
        private bool _stickCreated;
        private bool _isDragging;
        private Camera _mainCamera;
        private Vector3 _dragOffset;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _originalLocalPosition = transform.localPosition;
        }

        private void OnMouseDown()
        {
            if (_stickCreated) return;

            _isDragging = true;
            IsDraggingAny = true;
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            _dragOffset = transform.position - mouseWorldPos;
        }

        private void OnMouseDrag()
        {
            if (!_isDragging || _stickCreated) return;

            Vector3 mouseWorldPos = GetMouseWorldPosition();
            Vector3 targetPos = mouseWorldPos + _dragOffset;
            targetPos.z = transform.position.z;
            transform.position = targetPos;
        }

        private void Update()
        {
            if (!_isDragging) return;

            if (Input.GetMouseButtonUp(0))
                OnDragEnd();
        }

        private void OnDragEnd()
        {
            _isDragging = false;
            IsDraggingAny = false;

            if (_validZone != null && _validZone.OverlapPoint(transform.position))
                CreateStick();
            else
                StartCoroutine(SnapBackRoutine());
        }

        private void CreateStick()
        {
            _stickCreated = true;

            Vector3 start = transform.position;
            Vector3 end = _anchor5.position;
            Vector3 mid = (start + end) / 2f;
            float distance = Vector2.Distance(start, end);
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;

            GameObject stick = new GameObject("SettingsStick");
            stick.transform.position = mid;
            stick.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            stick.transform.SetParent(_stickParent != null ? _stickParent : transform.parent);

            // 碰撞体
            var collider = stick.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(distance, _stickThickness);

            // 贴图
            if (_stickSprite != null)
            {
                var sr = stick.AddComponent<SpriteRenderer>();
                sr.sprite = _stickSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(distance, _stickThickness);
                sr.sortingOrder = 0;
            }
        }

        private IEnumerator SnapBackRoutine()
        {
            Vector3 startPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < _snapBackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _snapBackDuration);
                t = t * t * (3f - 2f * t);
                transform.localPosition = Vector3.Lerp(startPos, _originalLocalPosition, t);
                yield return null;
            }

            transform.localPosition = _originalLocalPosition;
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector3 screenPos = Input.mousePosition;
            screenPos.z = -_mainCamera.transform.position.z;
            return _mainCamera.ScreenToWorldPoint(screenPos);
        }
    }
}

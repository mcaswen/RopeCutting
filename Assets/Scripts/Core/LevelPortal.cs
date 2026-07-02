using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    /// <summary>
    /// 传送口：过场动画结束后激活，检测角色进入后触发事件。
    /// 下一关可通过 OnCharacterEntered 在 Inspector 接线，
    /// 或通过代码获取 ILevelPortal 接口调用。
    /// </summary>
    public interface ILevelPortal
    {
        /// <summary>角色进入传送口时触发</summary>
        event UnityAction OnCharacterEntered;
        /// <summary>手动激活传送口</summary>
        void Activate();
    }

    [RequireComponent(typeof(Collider2D))]
    public class LevelPortal : MonoBehaviour, ILevelPortal
    {
        [Header("Settings")]
        [SerializeField] private string _targetTag = "Player";

        [Header("Events")]
        [SerializeField] private UnityEvent _onCharacterEntered;

        private Collider2D _collider;
        private bool _isActivated;

        public event UnityAction OnCharacterEntered;

        public void Activate()
        {
            _isActivated = true;
            if (_collider != null)
                _collider.enabled = true;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;
            _collider.enabled = false; // 默认禁用，由外部激活
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActivated) return;

            if (!string.IsNullOrEmpty(_targetTag) && !other.CompareTag(_targetTag))
                return;

            _onCharacterEntered?.Invoke();
            OnCharacterEntered?.Invoke();
        }
    }
}

using UnityEngine;
using UnityEngine.Events;
using Gameplay.Collectible;

namespace Gameplay.Character
{
    /// <summary>
    /// 目标角色，使用 Trigger 碰撞体检测糖果是否落入
    /// 检测到糖果进入时触发 OnCandyCollected 事件
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Character : MonoBehaviour
    {
        public UnityEvent OnCandyCollected;

        private void Awake()
        {
            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<Candy>(out _))
            {
                OnCandyCollected?.Invoke();
                Destroy(other.gameObject);
            }
        }
    }
}

using UnityEngine;
using Gameplay.Collectible;
using UI;

namespace Gameplay
{
    /// <summary>
    /// 地面失败检测器，Candy 掉落至触发器区域时显示失败
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FailDetector : MonoBehaviour
    {
        [SerializeField] private ResultPanel _resultPanel;

        private void Awake()
        {
            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<Candy>(out _))
            {
                _resultPanel.ShowDefeat();
                Destroy(other.gameObject);
            }
        }
    }
}

using UnityEngine;
using Core;
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

        private bool _lockedPlayerInput;

        private void Awake()
        {
            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<Candy>(out _))
            {
                LockPlayerInput();
                _resultPanel.ShowDefeat();
                Destroy(other.gameObject);
            }
        }

        private void OnDestroy()
        {
            UnlockPlayerInput();
        }

        private void LockPlayerInput()
        {
            if (_lockedPlayerInput) return;

            PlayerInputLock.Lock(this);
            _lockedPlayerInput = true;
        }

        private void UnlockPlayerInput()
        {
            if (!_lockedPlayerInput) return;

            PlayerInputLock.Unlock(this);
            _lockedPlayerInput = false;
        }
    }
}

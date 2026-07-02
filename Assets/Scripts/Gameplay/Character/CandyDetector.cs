using UnityEngine;
using Gameplay.Collectible;
using UI;

namespace Gameplay.Character
{
    /// <summary>
    /// 检测 Candy 是否进入角色周围的检测范围
    /// 控制父对象 Animator 的 IsCandyInRange 参数以切换 Idle / Mouse_Open 动画
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CandyDetector : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Character _character;
        [SerializeField] private ResultPanel _resultPanel;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInParent<Animator>();

            if (_character == null)
                _character = GetComponentInParent<Character>();

            if (_character != null)
                _character.OnCandyCollected.AddListener(OnCharacterCollectedCandy);

            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnDestroy()
        {
            if (_character != null)
                _character.OnCandyCollected.RemoveListener(OnCharacterCollectedCandy);
        }

        private void OnCharacterCollectedCandy()
        {
            _animator.SetBool("IsCandyInRange", false);
            if (_resultPanel != null)
                _resultPanel.ShowVictory();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<Candy>(out _))
            {
                _animator.SetBool("IsCandyInRange", true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent<Candy>(out _))
            {
                _animator.SetBool("IsCandyInRange", false);
            }
        }
    }
}

using UnityEngine;
using Gameplay.Character;

namespace Core
{
    /// <summary>
    /// 游戏管理器，管理游戏状态和流程
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private Character character;
        [SerializeField] private GameObject victoryPanel;

        private bool _isVictory;

        private void Start()
        {
            if (character != null)
            {
                character.OnCandyCollected.AddListener(OnVictory);
            }

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (character != null)
            {
                character.OnCandyCollected.RemoveListener(OnVictory);
            }
        }

        /// <summary>
        /// 处理胜利逻辑，显示结算面板
        /// </summary>
        private void OnVictory()
        {
            if (_isVictory) return;
            _isVictory = true;

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }

            Debug.Log("Victory! Candy collected!");
        }

        public bool IsVictory => _isVictory;
    }
}

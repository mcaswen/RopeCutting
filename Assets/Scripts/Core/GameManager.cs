using System.Collections;
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
        [SerializeField, Min(0f)] private float victoryPanelDelay = 1f;

        private bool _isVictory;
        private Coroutine _victoryRoutine;
        private bool _lockedPlayerInput;

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
            if (_victoryRoutine != null)
            {
                StopCoroutine(_victoryRoutine);
                _victoryRoutine = null;
            }

            UnlockPlayerInput();

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
            LockPlayerInput();

            if (victoryPanelDelay <= 0f)
            {
                ShowVictoryPanel();
                return;
            }

            _victoryRoutine = StartCoroutine(ShowVictoryPanelAfterDelay());
        }

        private IEnumerator ShowVictoryPanelAfterDelay()
        {
            yield return new WaitForSeconds(victoryPanelDelay);
            _victoryRoutine = null;
            ShowVictoryPanel();
        }

        private void ShowVictoryPanel()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }

            Debug.Log("Victory! Candy collected!");
        }

        public bool IsVictory => _isVictory;

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

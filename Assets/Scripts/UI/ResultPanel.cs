using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// 结算面板，显示胜利/失败结果并提供重新开始功能
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private string _victoryMessage = "胜利！";
        [SerializeField] private string _defeatMessage = "失败！";

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(RestartLevel);
            }
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(RestartLevel);
            }
        }

        /// <summary>
        /// 显示胜利结果
        /// </summary>
        public void ShowVictory()
        {
            gameObject.SetActive(true);
            if (_resultText != null)
            {
                _resultText.text = _victoryMessage;
            }
        }

        /// <summary>
        /// 显示失败结果
        /// </summary>
        public void ShowDefeat()
        {
            gameObject.SetActive(true);
            if (_resultText != null)
            {
                _resultText.text = _defeatMessage;
            }
        }

        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        private void RestartLevel()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;

namespace UI
{
    /// <summary>
    /// 结算面板，显示胜利/失败结果并提供重新开始功能
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private string _failureMessage = "Failed";
        [SerializeField] private string _victoryMessage = "胜利！";
        [SerializeField] private string _defeatMessage = "失败！";

        private bool _isShowingResult;

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

        private void OnDisable()
        {
            if (!_isShowingResult) return;

            _isShowingResult = false;
            PlayerInputLock.Clear();
        }

        /// <summary>
        /// 显示胜利结果
        /// </summary>
        public void ShowVictory()
        {
            _isShowingResult = true;
            gameObject.SetActive(true);
            if (_resultText != null)
            {
                _resultText.text = _victoryMessage;
            }
        }

        public void ShowFailure()
        {
            _isShowingResult = true;
            gameObject.SetActive(true);
            if (_resultText != null)
            {
                _resultText.text = _failureMessage;
            }
        }

        /// <summary>
        /// 显示失败结果
        /// </summary>
        public void ShowDefeat()
        {
            _isShowingResult = true;
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
            PlayerInputLock.Clear();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}

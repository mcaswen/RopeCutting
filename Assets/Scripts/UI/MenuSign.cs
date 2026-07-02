using UnityEngine;

namespace UI
{
    /// <summary>
    /// 菜单动作，与 MenuSign 配合使用
    /// </summary>
    public enum MenuAction
    {
        StartGame,
        ExitGame
    }

    /// <summary>
    /// 菜单牌子，被绳子悬挂，切割后掉落触发对应操作
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class MenuSign : MonoBehaviour
    {
        [SerializeField] private MenuAction _action;
        [SerializeField] private string _gameSceneName = "Scene_wanyun";
        [SerializeField] private string _groundTag = "Finish";

        private void Awake()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
        }

        /// <summary>
        /// 牌子掉入底部触发器区域时执行对应操作
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(_groundTag)) return;

            switch (_action)
            {
                case MenuAction.StartGame:
                    UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
                    break;
                case MenuAction.ExitGame:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }
    }
}

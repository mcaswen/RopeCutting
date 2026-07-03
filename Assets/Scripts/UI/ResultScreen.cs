using Core;
using UnityEngine;

namespace UI
{
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _restartAnchor;   // 含 Rope 子物体
        [SerializeField] private GameObject _restartSign;
        [SerializeField] private GameObject _nextLevelAnchor; // 含 Rope 子物体
        [SerializeField] private GameObject _nextLevelSign;

        public void ShowVictory()
        {
            // 解锁输入让 RopeCutter 能检测绳子切割
            PlayerInputLock.Clear();

            // 在激活父对象前只激活需要显示的牌子，另一个保持 inactive
            SetActive(_restartSign, false);
            SetActive(_nextLevelSign, true);
            // 不需要的 Anchor 也预先设为 inactive，避免其 RopeController 创建绳子
            SetActive(_restartAnchor, false);

            gameObject.SetActive(true);
            SetActive(_nextLevelAnchor, true);
        }

        public void ShowFailure()
        {
            PlayerInputLock.Clear();

            SetActive(_nextLevelSign, false);
            SetActive(_restartSign, true);
            SetActive(_nextLevelAnchor, false);

            gameObject.SetActive(true);
            SetActive(_restartAnchor, true);
        }

        private static void SetActive(GameObject obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}

using Gameplay.Collectible;
using UI;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 第二关流程控制器，复用基类胜利逻辑（Candy 被 Character 收集），
    /// 添加小狗特殊失败机制：狗吃到 Candy 时触发失败。
    /// </summary>
    public class LevelTwoFlowOverrider : LevelFlowController
    {
        [Header("Level 2 - Dog Failure")]
        [SerializeField] private Dog _dog;

        public void HandleDogAteCandy()
        {
            if (!IsPlaying) return;
            CompleteFailure();
        }
    }
}

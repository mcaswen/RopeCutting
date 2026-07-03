using Gameplay.Collectible;
using Gameplay.Rope;
using Systems.Dialogue;
using UI;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 第二关流程控制器，复用基类胜利逻辑（Candy 被 Character 收集），
    /// 添加小狗特殊失败机制：狗吃到 Candy 时触发失败。
    /// 支持指定绳子被切断时播放对应对话。
    /// </summary>
    public class LevelTwoFlowOverrider : LevelFlowController
    {
        [Header("Level 2 - Dog Failure")]
        [SerializeField] private Dog _dog;

        [Header("Level 2 - Rope Cut Dialogues")]
        [SerializeField] private RopeController _dogLeashRope;
        [SerializeField] private string _dogLeashCutDialogueId;
        [SerializeField] private RopeController _powerCableRope;
        [SerializeField] private string _powerCutDialogueId;

        public void HandleDogAteCandy()
        {
            if (!IsPlaying) return;
            CompleteFailure();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_dogLeashRope != null)
                _dogLeashRope.OnCut += HandleDogLeashCut;
            if (_powerCableRope != null)
                _powerCableRope.OnCut += HandlePowerCableCut;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_dogLeashRope != null)
                _dogLeashRope.OnCut -= HandleDogLeashCut;
            if (_powerCableRope != null)
                _powerCableRope.OnCut -= HandlePowerCableCut;
        }

        private void HandleDogLeashCut(RopeController rope)
        {
            if (!IsPlaying) return;
            if (string.IsNullOrWhiteSpace(_dogLeashCutDialogueId)) return;

            var subtitleUI = FindObjectOfType<DialogueSubtitleUI>(true);
            if (subtitleUI != null)
                subtitleUI.EnsureActive();
            DialogueManager.Instance?.Play(_dogLeashCutDialogueId);
        }

        private void HandlePowerCableCut(RopeController rope)
        {
            if (!IsPlaying) return;
            if (string.IsNullOrWhiteSpace(_powerCutDialogueId)) return;

            var subtitleUI = FindObjectOfType<DialogueSubtitleUI>(true);
            if (subtitleUI != null)
                subtitleUI.EnsureActive();
            DialogueManager.Instance?.Play(_powerCutDialogueId);
        }
    }
}

using UnityEngine;

namespace Gameplay.Rope
{
    /// <summary>
    /// 关卡动作锚点：绳子被割断时触发 UnityEvent。
    /// 用于结算界面的重新开始/下一关绳子。
    /// </summary>
    public class LevelActionAnchor : RopeAnchor
    {
        [SerializeField] private UnityEngine.Events.UnityEvent _onCut;
        [SerializeField] private bool _triggerOnce = true;

        private bool _hasTriggered;

        public override void OnRopeCut(RopeController sourceRope, Transform sourceAnchor, Vector3 hitPoint)
        {
            if (_triggerOnce && _hasTriggered) return;
            _hasTriggered = true;
            _onCut?.Invoke();
        }
    }
}

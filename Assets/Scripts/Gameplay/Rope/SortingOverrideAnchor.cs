using UnityEngine;

namespace Gameplay.Rope
{
    /// <summary>
    /// 排序层级覆盖锚点：切断指定绳子后修改目标锚点的 Order In Layer。
    /// 组件挂在绳子连接的 Anchor 上，OnRopeCut 回调触发。
    /// 支持一对多：一根绳子切断后调整多个锚点的层级。
    /// </summary>
    public class SortingOverrideAnchor : RopeAnchor
    {
        [System.Serializable]
        public struct TargetOverride
        {
            public Transform targetAnchor;
            public int sortingOrder;
        }

        [Header("Trigger")]
        [SerializeField] private RopeController _targetRope;

        [Header("Targets")]
        [SerializeField] private TargetOverride[] _targets;

        [Header("Options")]
        [SerializeField] private bool _triggerOnce = true;

        private bool _hasTriggered;

        public override void OnRopeCut(RopeController sourceRope, Transform sourceAnchor, Vector3 hitPoint)
        {
            if (_targetRope != null && sourceRope != _targetRope) return;
            if (_triggerOnce && _hasTriggered) return;
            _hasTriggered = true;

            foreach (TargetOverride target in _targets)
            {
                if (target.targetAnchor == null) continue;

                SpriteRenderer sr = target.targetAnchor.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = target.sortingOrder;
                }
            }
        }
    }
}

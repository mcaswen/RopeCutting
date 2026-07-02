using UnityEngine;

namespace Gameplay.Rope
{
    /// <summary>
    /// 联动掉落锚点：连接到该锚点的绳子被割断时，让另一条绳子从指定锚点脱落。
    /// </summary>
    public class LinkedDropAnchor : RopeAnchor
    {
        [SerializeField] private RopeController _ropeToDrop;
        [SerializeField] private Transform _anchorToDrop;
        [SerializeField] private bool _triggerOnce = true;

        private bool _hasTriggered;

        public override void OnRopeCut(RopeController sourceRope, Transform sourceAnchor, Vector3 hitPoint)
        {
            if (_triggerOnce && _hasTriggered) return;
            if (_ropeToDrop == null) return;

            Transform targetAnchor = _anchorToDrop != null ? _anchorToDrop : transform;
            _hasTriggered = true;
            _ropeToDrop.DropRopeFromAnchor(targetAnchor);
        }
    }
}

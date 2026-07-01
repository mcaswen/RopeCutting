using UnityEngine;

namespace Gameplay.Collectible
{
    /// <summary>
    /// 糖果控制器，管理物理连接
    /// 初始通过 DistanceJoint2D 连接到锚点
    /// 切割时销毁 Joint，由 RopeSegment 物理节点链牵引下落
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Candy : MonoBehaviour
    {
        private DistanceJoint2D _distanceJoint;
        private Rigidbody2D _rigidbody;

        private void Awake()
        {
            _distanceJoint = GetComponent<DistanceJoint2D>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.gravityScale = 1f;
        }

        /// <summary>
        /// 销毁连接 Joint，释放糖果使其受物理影响运动
        /// </summary>
        public void Release()
        {
            if (_distanceJoint != null)
            {
                Destroy(_distanceJoint);
            }
        }
    }
}

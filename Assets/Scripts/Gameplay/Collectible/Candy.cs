using UnityEngine;
using Gameplay.Rope;

namespace Gameplay.Collectible
{
    /// <summary>
    /// 糖果控制器，使用 Unity 2D 真实重力的可连接物。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Candy : RealGravityRopeConnectable
    {
        /// <summary>
        /// 销毁连接 Joint，释放糖果使其受物理影响运动
        /// </summary>
        public void Release()
        {
            ReleaseInitialConnection();
        }
    }
}

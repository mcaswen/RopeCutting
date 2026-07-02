using UnityEngine;

namespace Gameplay.Rope
{
    /// <summary>
    /// 绳子锚点基类，用于接收连接在该锚点上的绳子切断事件。
    /// </summary>
    public class RopeAnchor : MonoBehaviour
    {
        public virtual void OnRopeCut(RopeController sourceRope, Transform sourceAnchor, Vector3 hitPoint)
        {
        }
    }
}

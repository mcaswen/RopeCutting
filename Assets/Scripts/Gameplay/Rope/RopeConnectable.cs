using UnityEngine;

namespace Gameplay.Rope
{
    /// <summary>
    /// 可以被绳子连接的物品基类，负责提供挂点和切断回调。
    /// </summary>
    public abstract class RopeConnectable : MonoBehaviour
    {
        [SerializeField] private float _attachRadius = 0.35f;
        [SerializeField] private bool _useDirectionalAttachPoint = true;

        private DistanceJoint2D[] _initialJoints;
        private int _activeRopeCount;

        public virtual Rigidbody2D Rigidbody => null;
        public int ActiveRopeCount => _activeRopeCount;

        protected virtual void Awake()
        {
            _initialJoints = GetComponents<DistanceJoint2D>();
        }

        public virtual void ReleaseInitialConnection()
        {
            if (_initialJoints == null) return;

            for (int i = 0; i < _initialJoints.Length; i++)
            {
                if (_initialJoints[i] != null)
                    Destroy(_initialJoints[i]);
            }

            _initialJoints = null;
        }

        public virtual Vector2 GetLocalAttachPoint(Vector3 anchorWorldPosition)
        {
            if (!_useDirectionalAttachPoint || _attachRadius <= 0f)
                return Vector2.zero;

            Vector2 worldDirection = (Vector2)anchorWorldPosition - (Vector2)transform.position;
            if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
                worldDirection = Vector2.up;

            Vector2 worldOffset = worldDirection.normalized * _attachRadius;
            return transform.InverseTransformVector(worldOffset);
        }

        public Vector3 GetWorldAttachPoint(Vector2 localAttachPoint)
        {
            return transform.TransformPoint(localAttachPoint);
        }

        public void RegisterRope()
        {
            _activeRopeCount++;
        }

        public void NotifyRopeCut(Vector3 hitPoint)
        {
            _activeRopeCount = Mathf.Max(0, _activeRopeCount - 1);
            OnRopeCut(hitPoint, _activeRopeCount);

            if (_activeRopeCount == 0)
                OnAllRopesCut(hitPoint);
        }

        protected virtual void OnRopeCut(Vector3 hitPoint, int remainingRopeCount)
        {
        }

        protected virtual void OnAllRopesCut(Vector3 hitPoint)
        {
        }
    }
}

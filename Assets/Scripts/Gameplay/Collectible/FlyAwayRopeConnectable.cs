using UnityEngine;
using Gameplay.Rope;

namespace Gameplay.Collectible
{
    /// <summary>
    /// 绳子被切断后会获得冲量飞走的可连接物。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FlyAwayRopeConnectable : RealGravityRopeConnectable
    {
        [SerializeField] private bool _flyOnlyWhenAllRopesCut;
        [SerializeField] private bool _useDirectionAwayFromCut = true;
        [SerializeField] private Vector2 _flyDirection = Vector2.up;
        [SerializeField] private float _flyForce = 8f;
        [SerializeField] private float _torqueImpulse = 3f;
        [SerializeField] private bool _disableGravityAfterFly;
        [SerializeField] private float _destroyAfterSeconds = -1f;

        private bool _hasFlown;

        protected override void OnRopeCut(Vector3 hitPoint, int remainingRopeCount)
        {
            if (!_flyOnlyWhenAllRopesCut)
                Fly(hitPoint);
        }

        protected override void OnAllRopesCut(Vector3 hitPoint)
        {
            if (_flyOnlyWhenAllRopesCut)
                Fly(hitPoint);
        }

        private void Fly(Vector3 hitPoint)
        {
            if (_hasFlown || Body == null) return;

            _hasFlown = true;

            Vector2 direction = _useDirectionAwayFromCut
                ? (Vector2)(transform.position - hitPoint)
                : _flyDirection;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector2.up;

            if (_disableGravityAfterFly)
                Body.gravityScale = 0f;

            Body.AddForce(direction.normalized * _flyForce, ForceMode2D.Impulse);

            if (!Mathf.Approximately(_torqueImpulse, 0f))
                Body.AddTorque(_torqueImpulse, ForceMode2D.Impulse);

            if (_destroyAfterSeconds > 0f)
                Destroy(gameObject, _destroyAfterSeconds);
        }
    }
}

using UnityEngine;
using Gameplay.Rope;

namespace Gameplay.Collectible
{
    /// <summary>
    /// 不使用 Rigidbody2D 控制的可连接物；绳子断开后由脚本模拟重力加速度。
    /// </summary>
    public class ManualGravityRopeConnectable : RopeConnectable
    {
        [SerializeField] private bool _fallOnlyWhenAllRopesCut = true;
        [SerializeField] private Vector2 _initialVelocity;
        [SerializeField] private Vector2 _gravity = new Vector2(0f, -9.81f);
        [SerializeField] private float _maxFallSpeed = 25f;
        [SerializeField] private float _destroyAfterSeconds = -1f;

        private Vector2 _velocity;
        private bool _isFalling;

        private void Update()
        {
            if (!_isFalling) return;

            _velocity += _gravity * Time.deltaTime;

            if (_maxFallSpeed > 0f && _velocity.magnitude > _maxFallSpeed)
                _velocity = _velocity.normalized * _maxFallSpeed;

            transform.position += (Vector3)(_velocity * Time.deltaTime);
        }

        protected override void OnRopeCut(Vector3 hitPoint, int remainingRopeCount)
        {
            if (!_fallOnlyWhenAllRopesCut)
                StartFalling();
        }

        protected override void OnAllRopesCut(Vector3 hitPoint)
        {
            StartFalling();
        }

        private void StartFalling()
        {
            if (_isFalling) return;

            _isFalling = true;
            _velocity = _initialVelocity;

            if (_destroyAfterSeconds > 0f)
                Destroy(gameObject, _destroyAfterSeconds);
        }
    }
}

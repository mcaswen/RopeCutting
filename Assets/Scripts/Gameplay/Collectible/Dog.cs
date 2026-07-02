using Core;
using Gameplay.Rope;
using Systems;
using UnityEngine;

namespace Gameplay.Collectible
{
    /// <summary>
    /// 小狗：绳子被割断后向左跑，碰到 Candy 时触发失败。
    /// 使用 Rigidbody2D 物理速度实现运动。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
    public class Dog : RopeConnectable
    {
        [SerializeField] private float _runSpeed = 5f;
        [SerializeField] private Vector2 _runDirection = Vector2.left;
        [SerializeField] private LevelTwoFlowOverrider _levelFlow;

        private Rigidbody2D _rigidbody;
        private Animator _animator;
        private bool _isRunning;

        public override Rigidbody2D Rigidbody => _rigidbody;

        protected override void Awake()
        {
            base.Awake();
            _rigidbody = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        private void FixedUpdate()
        {
            if (!_isRunning) return;

            _rigidbody.velocity = _runDirection.normalized * _runSpeed;
        }

        protected override void OnAllRopesCut(Vector3 hitPoint)
        {
            StartRunning();
        }

        private void StartRunning()
        {
            if (_isRunning) return;
            _isRunning = true;

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.velocity = _runDirection.normalized * _runSpeed;

            if (_animator != null)
                _animator.SetBool("IsRunning", true);

            SfxPlayer.Play(SfxId.Bark);
            ReleaseInitialConnection();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isRunning) return;

            if (other.TryGetComponent<Candy>(out _))
            {
                if (_levelFlow != null)
                    _levelFlow.HandleDogAteCandy();
                Destroy(other.gameObject);
            }
        }
    }
}

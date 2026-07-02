using Gameplay.Rope;
using Systems;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.Collectible
{
    public class FlyingCharacterRopeConnectable : RopeConnectable
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _flyTrigger = "Fly";
        [SerializeField] private string _flyBool = "IsFlying";
        [SerializeField] private string _flyStateName;
        [SerializeField] private Vector2 _flyVelocity = new Vector2(0f, 4f);
        [SerializeField] private float _flyAcceleration = 0f;
        [SerializeField] private float _maxFlySpeed = 8f;
        [SerializeField] private float _destroyAfterSeconds = -1f;
        [SerializeField] private UnityEvent _onFlyStarted;

        private Vector2 _velocity;
        private bool _isFlying;

        protected override void Awake()
        {
            base.Awake();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (!_isFlying) return;

            if (!Mathf.Approximately(_flyAcceleration, 0f))
            {
                _velocity += Vector2.up * (_flyAcceleration * Time.deltaTime);

                if (_maxFlySpeed > 0f && _velocity.magnitude > _maxFlySpeed)
                    _velocity = _velocity.normalized * _maxFlySpeed;
            }

            transform.position += (Vector3)(_velocity * Time.deltaTime);
        }

        protected override void OnAllRopesCut(Vector3 hitPoint)
        {
            StartFlying();
        }

        private void StartFlying()
        {
            if (_isFlying) return;

            _isFlying = true;
            _velocity = _flyVelocity;

            PlayFlyAnimation();
            SfxPlayer.Play(SfxId.WingFlap);
            _onFlyStarted?.Invoke();

            if (_destroyAfterSeconds > 0f)
                Destroy(gameObject, _destroyAfterSeconds);
        }

        private void PlayFlyAnimation()
        {
            if (_animator == null) return;

            if (!string.IsNullOrEmpty(_flyTrigger))
                _animator.SetTrigger(_flyTrigger);

            if (!string.IsNullOrEmpty(_flyBool))
                _animator.SetBool(_flyBool, true);

            if (!string.IsNullOrEmpty(_flyStateName))
                _animator.Play(_flyStateName);
        }
    }
}

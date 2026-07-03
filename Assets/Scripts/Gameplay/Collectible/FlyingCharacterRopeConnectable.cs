using System;
using System.Collections;
using Gameplay.Rope;
using Systems;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Gameplay.Collectible
{
    public class FlyingCharacterRopeConnectable : RopeConnectable
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private RuntimeAnimatorController _flyController;
        [SerializeField] private bool _preferSpriteRendererAnimator = true;
        [SerializeField] private bool _disableOtherAnimatorsWhenFlying = true;
        [SerializeField] private string _flyTrigger = "Fly";
        [SerializeField] private string _flyBool = "IsFlying";
        [SerializeField] private string _flyStateName;
        [SerializeField, Min(0f)] private float _movementDelay = 1f;
        [SerializeField] private Vector2 _flyVelocity = new Vector2(0f, 4f);
        [SerializeField] private float _flyAcceleration = 0f;
        [SerializeField] private float _maxFlySpeed = 8f;
        [FormerlySerializedAs("_destroyAfterSeconds")]
        [SerializeField] private float _disappearAfterSeconds = 0.1f;
        [SerializeField] private UnityEvent _onFlyStarted;
        [SerializeField] private UnityEvent _onDisappearing;

        private Vector2 _velocity;
        private bool _isFlying;
        private float _flyMoveTimer;
        private RuntimeAnimatorController _controllerFromAssignedAnimator;
        private Coroutine _disappearRoutine;

        public event Action<FlyingCharacterRopeConnectable> Disappearing;

        protected override void Awake()
        {
            base.Awake();

            ResolveAnimator();
        }

        private void Update()
        {
            if (!_isFlying) return;

            if (_flyMoveTimer > 0f)
            {
                _flyMoveTimer -= Time.deltaTime;
                return;
            }

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
            _flyMoveTimer = _movementDelay;

            PlayFlyAnimation();
            SfxPlayer.Play(SfxId.WingFlap);
            _onFlyStarted?.Invoke();

            if (_disappearAfterSeconds > 0f)
            {
                if (_disappearRoutine != null)
                    StopCoroutine(_disappearRoutine);

                _disappearRoutine = StartCoroutine(DisappearAfterDelay());
            }
        }

        private IEnumerator DisappearAfterDelay()
        {
            yield return new WaitForSeconds(_disappearAfterSeconds);

            Disappearing?.Invoke(this);
            _onDisappearing?.Invoke();
            Destroy(gameObject);
        }

        private void PlayFlyAnimation()
        {
            if (_animator == null) return;

            if (_flyController != null)
                _animator.runtimeAnimatorController = _flyController;

            if (_disableOtherAnimatorsWhenFlying)
                DisableOtherAnimators();

            if (!string.IsNullOrEmpty(_flyTrigger) && HasAnimatorParameter(_flyTrigger, AnimatorControllerParameterType.Trigger))
                _animator.SetTrigger(_flyTrigger);

            if (!string.IsNullOrEmpty(_flyBool) && HasAnimatorParameter(_flyBool, AnimatorControllerParameterType.Bool))
                _animator.SetBool(_flyBool, true);

            if (!string.IsNullOrEmpty(_flyStateName))
                _animator.Play(_flyStateName, 0, 0f);
            else if (_flyController != null)
                _animator.Play(0, 0, 0f);
        }

        private void ResolveAnimator()
        {
            Animator assignedAnimator = _animator;
            if (assignedAnimator != null)
                _controllerFromAssignedAnimator = assignedAnimator.runtimeAnimatorController;

            if (_preferSpriteRendererAnimator)
            {
                Animator spriteAnimator = FindSpriteRendererAnimator();
                if (spriteAnimator != null)
                    _animator = spriteAnimator;
            }

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_flyController == null && _controllerFromAssignedAnimator != null && _controllerFromAssignedAnimator != _animator.runtimeAnimatorController)
                _flyController = _controllerFromAssignedAnimator;
        }

        private Animator FindSpriteRendererAnimator()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i].GetComponent<SpriteRenderer>() != null)
                    return animators[i];
            }

            return null;
        }

        private void DisableOtherAnimators()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i] != _animator)
                    animators[i].enabled = false;
            }
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (_animator == null || string.IsNullOrEmpty(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == parameterName)
                    return true;
            }

            return false;
        }
    }
}

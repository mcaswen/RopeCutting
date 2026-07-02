using UnityEngine;

namespace Gameplay.Rope
{
    /// <summary>
    /// 使用 Unity 2D 真实物理重力的可连接物。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class RealGravityRopeConnectable : RopeConnectable
    {
        [SerializeField] private bool _forceDynamicBody = true;
        [SerializeField] private float _gravityScale = 1f;

        private Rigidbody2D _rigidbody;

        public override Rigidbody2D Rigidbody => _rigidbody;
        protected Rigidbody2D Body => _rigidbody;

        protected override void Awake()
        {
            base.Awake();

            _rigidbody = GetComponent<Rigidbody2D>();

            if (_forceDynamicBody)
                _rigidbody.bodyType = RigidbodyType2D.Dynamic;

            _rigidbody.gravityScale = _gravityScale;
        }
    }
}

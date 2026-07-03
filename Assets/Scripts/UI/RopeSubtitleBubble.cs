using Gameplay.Rope;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 可被绳子连接的字幕气泡，文本尺寸交给 AdaptiveSubtitleBubble 处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class RopeSubtitleBubble : RealGravityRopeConnectable
    {
        [SerializeField] private AdaptiveSubtitleBubble _adaptiveBubble;
        [SerializeField] private bool _freezeRotation = true;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
        }
#endif

        public void Bind(AdaptiveSubtitleBubble adaptiveBubble)
        {
            if (adaptiveBubble != null)
                _adaptiveBubble = adaptiveBubble;

            ResolveReferences();
        }

        public void SetText(string text)
        {
            ResolveReferences();

            if (_adaptiveBubble != null)
                _adaptiveBubble.SetText(text);
        }

        public void PrepareForDrop(Vector2 initialVelocity, float gravityScale)
        {
            Rigidbody2D body = Rigidbody;
            if (body == null) return;

            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.velocity = initialVelocity;
            body.angularVelocity = 0f;
            body.constraints = _freezeRotation ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.None;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;

            Rigidbody2D body = Rigidbody;
            if (body != null)
                body.position = (Vector2)worldPosition;
        }

        public void FixAt(Vector3 worldPosition, bool freezeAll)
        {
            MoveTo(worldPosition);

            Rigidbody2D body = Rigidbody;
            if (body == null) return;

            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = freezeAll
                ? RigidbodyConstraints2D.FreezeAll
                : (_freezeRotation ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.None);
        }

        private void ResolveReferences()
        {
            if (_adaptiveBubble == null)
                _adaptiveBubble = GetComponent<AdaptiveSubtitleBubble>();

            if (_adaptiveBubble == null)
                _adaptiveBubble = GetComponentInChildren<AdaptiveSubtitleBubble>(true);
        }
    }
}

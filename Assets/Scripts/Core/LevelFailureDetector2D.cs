using UnityEngine;

namespace Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class LevelFailureDetector2D : MonoBehaviour
    {
        [SerializeField] private LevelFlowController _levelFlow;
        [SerializeField] private bool _logCollisions = true;

        private void Reset()
        {
            Collider2D failureCollider = GetComponent<Collider2D>();
            if (failureCollider != null)
                failureCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            NotifyLevelFlow(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            NotifyLevelFlow(collision.collider);
        }

        private void NotifyLevelFlow(Collider2D other)
        {
            if (_logCollisions)
            {
                string otherName = other != null ? other.name : "null";
                string flowName = _levelFlow != null ? _levelFlow.name : "null";
                Debug.Log($"[LevelFailureDetector2D] {name} hit {otherName}, flow={flowName}", this);
            }

            if (_levelFlow != null)
                _levelFlow.HandleFailureDetectorEnter(other);
        }
    }
}

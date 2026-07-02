using UnityEngine;
using System.Collections.Generic;
using Gameplay.Collectible;
using UnityEngine.Serialization;

namespace Gameplay.Rope
{
    /// <summary>
    /// 绳子控制器，管理物理节点链 + LineRenderer 视觉
    /// 绳子由一串 Rigidbody2D 节点通过 DistanceJoint2D 串联，
    /// LineRenderer 追踪节点位置并用 Sprite 纹理渲染出连续绳子
    /// 切割时销毁目标关节，物理系统自然拆分两端
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeController : MonoBehaviour
    {
        [SerializeField] private Transform _anchorPoint;
        [SerializeField] private Transform[] _anchorPoints;
        [FormerlySerializedAs("_candy")]
        [SerializeField] private RopeConnectable _connectable;

        [SerializeField] private int _nodeCount = 15;
        [SerializeField] private float _nodeMass = 0.05f;
        [SerializeField] private float _nodeRadius = 0.15f;
        [SerializeField] private float _ropeWidth = 0.08f;
        [SerializeField, Min(0f)] private float _cutFadeDelay = 0.15f;
        [SerializeField, Min(0.01f)] private float _cutFadeDuration = 0.4f;

        private LineRenderer _lineRenderer;
        private readonly List<RopeChain> _chains = new List<RopeChain>();

        private class RopeChain
        {
            public Transform AnchorPoint;
            public Vector2 ConnectableLocalAttachPoint;
            public LineRenderer LineRenderer;
            public Gradient OriginalGradient;
            public DistanceJoint2D AnchorJoint;
            public DistanceJoint2D ConnectableJoint;
            public bool UsesWorldConnectableAnchor;
            public readonly List<GameObject> Nodes = new List<GameObject>();
            public bool IsCut;
            public bool IsFadingOut;
            public bool FadeComplete;
            public float FadeTimer;
        }

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        private void Start()
        {
            CreateRopeChains();
        }

        /// <summary>
        /// 创建物理节点链：每个锚点 → 节点[0..N-1] → 同一个可连接物
        /// </summary>
        private void CreateRopeChains()
        {
            if (_nodeCount <= 0 || _connectable == null) return;

            List<Transform> anchors = GetAnchorPoints();
            if (anchors.Count == 0) return;

            _connectable.ReleaseInitialConnection();
            Rigidbody2D connectableRb = _connectable.Rigidbody;

            for (int i = 0; i < anchors.Count; i++)
            {
                RopeChain chain = new RopeChain
                {
                    AnchorPoint = anchors[i],
                    ConnectableLocalAttachPoint = _connectable.GetLocalAttachPoint(anchors[i].position),
                    LineRenderer = CreateLineRenderer(i)
                };

                CreateRopeChain(chain, connectableRb, i);
                SetupLineRenderer(chain.LineRenderer);
                chain.OriginalGradient = RopeLineFade.CloneGradient(chain.LineRenderer.colorGradient);
                _chains.Add(chain);
                _connectable.RegisterRope();
            }
        }

        private List<Transform> GetAnchorPoints()
        {
            List<Transform> anchors = new List<Transform>();

            if (_anchorPoint != null)
            {
                anchors.Add(_anchorPoint);
            }

            if (_anchorPoints != null)
            {
                for (int i = 0; i < _anchorPoints.Length; i++)
                {
                    Transform anchor = _anchorPoints[i];
                    if (anchor != null && !anchors.Contains(anchor))
                    {
                        anchors.Add(anchor);
                    }
                }
            }

            return anchors;
        }

        private LineRenderer CreateLineRenderer(int chainIndex)
        {
            if (chainIndex == 0)
                return _lineRenderer;

            GameObject lineObject = new GameObject("RopeLine_" + chainIndex);
            lineObject.transform.SetParent(transform, false);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            CopyLineRendererSettings(_lineRenderer, lineRenderer);
            return lineRenderer;
        }

        private void CopyLineRendererSettings(LineRenderer source, LineRenderer target)
        {
            target.sharedMaterial = source.sharedMaterial;
            target.colorGradient = source.colorGradient;
            target.alignment = source.alignment;
            target.numCapVertices = source.numCapVertices;
            target.numCornerVertices = source.numCornerVertices;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.textureMode = source.textureMode;
            target.useWorldSpace = source.useWorldSpace;
        }

        private void CreateRopeChain(RopeChain chain, Rigidbody2D connectableRb, int chainIndex)
        {
            // 确保锚点有 Kinematic Rigidbody2D（关节需要）
            Rigidbody2D anchorRb = chain.AnchorPoint.GetComponent<Rigidbody2D>();
            if (anchorRb == null)
            {
                anchorRb = chain.AnchorPoint.gameObject.AddComponent<Rigidbody2D>();
            }
            anchorRb.bodyType = RigidbodyType2D.Kinematic;

            Vector2 anchorPos = chain.AnchorPoint.position;
            Vector2 connectablePos = GetConnectableAttachWorldPoint(chain);
            float segmentDistance = Vector2.Distance(anchorPos, connectablePos) / (_nodeCount + 1);

            // 创建所有节点（物理组件 + 碰撞体，不含 SpriteRenderer）
            for (int i = 0; i < _nodeCount; i++)
            {
                float t = (i + 1) / (float)(_nodeCount + 1);
                Vector2 pos = Vector2.Lerp(anchorPos, connectablePos, t);

                GameObject node = new GameObject("RopeNode_" + chainIndex + "_" + i);
                node.transform.position = pos;
                node.transform.SetParent(transform);

                Rigidbody2D rb = node.AddComponent<Rigidbody2D>();
                rb.mass = _nodeMass;
                rb.drag = 0.5f;
                rb.gravityScale = 1f;

                CircleCollider2D col = node.AddComponent<CircleCollider2D>();
                col.radius = _nodeRadius;

                chain.Nodes.Add(node);

                // 忽略相邻节点碰撞
                if (i > 0)
                {
                    Physics2D.IgnoreCollision(
                        node.GetComponent<CircleCollider2D>(),
                        chain.Nodes[i - 1].GetComponent<CircleCollider2D>()
                    );
                }
            }

            // 设置关节连接
            DistanceJoint2D anchorJoint = chain.AnchorPoint.gameObject.AddComponent<DistanceJoint2D>();
            anchorJoint.connectedBody = chain.Nodes[0].GetComponent<Rigidbody2D>();
            anchorJoint.autoConfigureDistance = false;
            anchorJoint.distance = segmentDistance;
            chain.AnchorJoint = anchorJoint;

            for (int i = 0; i < _nodeCount; i++)
            {
                DistanceJoint2D joint = chain.Nodes[i].AddComponent<DistanceJoint2D>();

                if (i < _nodeCount - 1)
                {
                    joint.connectedBody = chain.Nodes[i + 1].GetComponent<Rigidbody2D>();
                }
                else
                {
                    if (connectableRb != null)
                    {
                        joint.connectedBody = connectableRb;
                        joint.connectedAnchor = chain.ConnectableLocalAttachPoint;
                    }
                    else
                    {
                        joint.connectedBody = null;
                        joint.connectedAnchor = GetConnectableAttachWorldPoint(chain);
                        chain.UsesWorldConnectableAnchor = true;
                    }

                    joint.autoConfigureConnectedAnchor = false;
                    chain.ConnectableJoint = joint;
                }

                joint.autoConfigureDistance = false;
                joint.distance = segmentDistance;
            }
        }

        /// <summary>
        /// 配置 LineRenderer 宽度与纹理模式
        /// </summary>
        private void SetupLineRenderer(LineRenderer lineRenderer)
        {
            lineRenderer.positionCount = _nodeCount + 2;
            lineRenderer.startWidth = _ropeWidth;
            lineRenderer.endWidth = _ropeWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.textureMode = LineTextureMode.Tile;
        }

        private void Update()
        {
            if (_chains.Count == 0) return;

            // 可连接物被销毁时清理整条绳子
            if (_connectable == null)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < _chains.Count; i++)
            {
                UpdateLineRenderer(_chains[i]);
            }
        }

        private void UpdateLineRenderer(RopeChain chain)
        {
            if (chain.FadeComplete || chain.LineRenderer == null) return;

            UpdateWorldConnectableJoint(chain);

            Vector3[] positions = GetRopePositions(chain, !chain.IsCut);
            bool hasVisibleRope = positions.Length > 1;

            chain.LineRenderer.enabled = hasVisibleRope;
            if (!hasVisibleRope)
            {
                if (chain.IsCut)
                    CompleteChainFade(chain);
                return;
            }

            if (chain.LineRenderer.positionCount != positions.Length)
                chain.LineRenderer.positionCount = positions.Length;

            for (int i = 0; i < positions.Length; i++)
                chain.LineRenderer.SetPosition(i, positions[i]);

            UpdateChainFade(chain);
        }

        private void UpdateWorldConnectableJoint(RopeChain chain)
        {
            if (!chain.UsesWorldConnectableAnchor || chain.IsCut || chain.ConnectableJoint == null)
                return;

            chain.ConnectableJoint.connectedAnchor = GetConnectableAttachWorldPoint(chain);
        }

        /// <summary>
        /// 获取绳子所有节点位置，用于切割检测
        /// </summary>
        public Vector3[] GetRopePositions()
        {
            if (_chains.Count == 0)
                return new Vector3[0];

            return GetRopePositions(_chains[0], !_chains[0].IsCut);
        }

        private Vector3[] GetRopePositions(RopeChain chain, bool includeConnectable)
        {
            int pointCount = chain.Nodes.Count + 1 + (includeConnectable ? 1 : 0);
            Vector3[] positions = new Vector3[pointCount];

            positions[0] = chain.AnchorPoint.position;
            for (int i = 0; i < chain.Nodes.Count; i++)
                positions[i + 1] = chain.Nodes[i].transform.position;

            if (includeConnectable)
                positions[pointCount - 1] = GetConnectableAttachWorldPoint(chain);

            return positions;
        }

        private Vector3 GetConnectableAttachWorldPoint(RopeChain chain)
        {
            if (_connectable == null)
                return Vector3.zero;

            return _connectable.GetWorldAttachPoint(chain.ConnectableLocalAttachPoint);
        }

        public bool TryCut(Vector2 lineStart, Vector2 lineEnd)
        {
            for (int chainIndex = 0; chainIndex < _chains.Count; chainIndex++)
            {
                RopeChain chain = _chains[chainIndex];
                if (chain.IsCut) continue;

                Vector3[] ropePositions = GetRopePositions(chain, true);
                for (int segmentIndex = 0; segmentIndex < ropePositions.Length - 1; segmentIndex++)
                {
                    Vector2 ropeSegStart = ropePositions[segmentIndex];
                    Vector2 ropeSegEnd = ropePositions[segmentIndex + 1];

                    if (SegmentsIntersect(lineStart, lineEnd, ropeSegStart, ropeSegEnd))
                    {
                        Vector2 hitPoint = GetIntersectionPoint(lineStart, lineEnd, ropeSegStart, ropeSegEnd);
                        CutChain(chain, hitPoint, segmentIndex);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool DropRopeFromAnchor(Transform anchorPoint)
        {
            if (anchorPoint == null) return false;

            RopeChain chain = FindChain(anchorPoint);
            if (chain == null || chain.IsCut) return false;

            CutChain(chain, anchorPoint.position, 0);
            return true;
        }

        /// <summary>
        /// 切割绳子，在切割点拆分为两段
        /// </summary>
        /// <param name="segmentIndex">切割命中的线段索引（Node[i] 与 Node[i+1] 之间的线段）</param>
        public void Cut(Vector3 hitPoint, int segmentIndex)
        {
            if (_chains.Count == 0) return;

            CutChain(_chains[0], hitPoint, segmentIndex);
        }

        private void CutChain(RopeChain chain, Vector3 hitPoint, int segmentIndex)
        {
            if (chain.IsCut || _connectable == null || chain.AnchorPoint == null) return;

            int nodeCount = chain.Nodes.Count;
            if (segmentIndex < 0 || segmentIndex > nodeCount) return;

            chain.IsCut = true;

            // segmentIndex 0 是锚点到第一个节点；之后是节点之间；最后一段是末节点到可连接物。
            DistanceJoint2D jointToDestroy = segmentIndex == 0
                ? chain.AnchorJoint
                : chain.Nodes[segmentIndex - 1].GetComponent<DistanceJoint2D>();
            if (jointToDestroy != null)
            {
                Destroy(jointToDestroy);
            }

            _connectable.NotifyRopeCut(hitPoint);
            NotifyAnchorCut(chain, hitPoint);

            // 下半段节点构建为 RopeSegment（含 LineRenderer）
            List<GameObject> lowerNodes = new List<GameObject>();
            for (int i = segmentIndex; i < chain.Nodes.Count; i++)
            {
                lowerNodes.Add(chain.Nodes[i]);
            }

            bool keepLowerSegmentConnectedToItem = _connectable.Rigidbody != null;

            if (!keepLowerSegmentConnectedToItem && chain.ConnectableJoint != null)
            {
                Destroy(chain.ConnectableJoint);
                chain.ConnectableJoint = null;
            }

            if (lowerNodes.Count > 0)
            {
                GameObject lowerGO = new GameObject(gameObject.name + "_下半段");
                lowerGO.transform.SetParent(transform);

                RopeSegment lowerSegment = lowerGO.AddComponent<RopeSegment>();
                lowerSegment.Initialize(
                    lowerNodes,
                    _ropeWidth,
                    chain.LineRenderer.sharedMaterial,
                    chain.OriginalGradient,
                    _cutFadeDelay,
                    _cutFadeDuration,
                    keepLowerSegmentConnectedToItem ? _connectable.transform : null,
                    chain.ConnectableLocalAttachPoint);

                // 从上半段列表中移除下半段节点
                chain.Nodes.RemoveRange(segmentIndex, lowerNodes.Count);
            }

            BeginChainFade(chain);
        }

        private void BeginChainFade(RopeChain chain)
        {
            if (chain == null || chain.FadeComplete) return;

            chain.IsFadingOut = true;
            chain.FadeTimer = 0f;
        }

        private void UpdateChainFade(RopeChain chain)
        {
            if (!chain.IsFadingOut || chain.FadeComplete) return;

            chain.FadeTimer += Time.deltaTime;

            float fadeElapsed = Mathf.Max(0f, chain.FadeTimer - _cutFadeDelay);
            float alphaMultiplier = 1f - Mathf.Clamp01(fadeElapsed / Mathf.Max(0.01f, _cutFadeDuration));
            RopeLineFade.ApplyAlpha(chain.LineRenderer, chain.OriginalGradient, alphaMultiplier);

            if (fadeElapsed >= _cutFadeDuration)
                CompleteChainFade(chain);
        }

        private void CompleteChainFade(RopeChain chain)
        {
            if (chain == null || chain.FadeComplete) return;

            chain.FadeComplete = true;

            if (chain.LineRenderer != null)
                chain.LineRenderer.enabled = false;

            if (chain.AnchorJoint != null)
            {
                Destroy(chain.AnchorJoint);
                chain.AnchorJoint = null;
            }

            for (int i = chain.Nodes.Count - 1; i >= 0; i--)
            {
                if (chain.Nodes[i] != null)
                    Destroy(chain.Nodes[i]);
            }

            chain.Nodes.Clear();
        }

        private RopeChain FindChain(Transform anchorPoint)
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                if (_chains[i].AnchorPoint == anchorPoint)
                    return _chains[i];
            }

            return null;
        }

        private void NotifyAnchorCut(RopeChain chain, Vector3 hitPoint)
        {
            RopeAnchor anchor = chain.AnchorPoint.GetComponent<RopeAnchor>();
            if (anchor != null)
                anchor.OnRopeCut(this, chain.AnchorPoint, hitPoint);
        }

        private Vector2 GetIntersectionPoint(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float denominator = (b2.y - b1.y) * (a2.x - a1.x) - (b2.x - b1.x) * (a2.y - a1.y);
            if (Mathf.Approximately(denominator, 0))
                return (a1 + a2) / 2;

            float ua = ((b2.x - b1.x) * (a1.y - b1.y) - (b2.y - b1.y) * (a1.x - b1.x)) / denominator;
            return new Vector2(a1.x + ua * (a2.x - a1.x), a1.y + ua * (a2.y - a1.y));
        }

        private bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float o1 = Orientation(a1, a2, b1);
            float o2 = Orientation(a1, a2, b2);
            float o3 = Orientation(b1, b2, a1);
            float o4 = Orientation(b1, b2, a2);

            return o1 * o2 < 0 && o3 * o4 < 0;
        }

        private float Orientation(Vector2 p, Vector2 q, Vector2 r)
        {
            return (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
        }

        private void OnDestroy()
        {
            for (int chainIndex = _chains.Count - 1; chainIndex >= 0; chainIndex--)
            {
                RopeChain chain = _chains[chainIndex];
                if (chain.AnchorJoint != null)
                {
                    Destroy(chain.AnchorJoint);
                }

                for (int i = chain.Nodes.Count - 1; i >= 0; i--)
                {
                    if (chain.Nodes[i] != null)
                    {
                        Destroy(chain.Nodes[i]);
                    }
                }
            }
        }

        public bool IsCut
        {
            get
            {
                if (_chains.Count == 0) return false;

                for (int i = 0; i < _chains.Count; i++)
                {
                    if (!_chains[i].IsCut)
                        return false;
                }

                return true;
            }
        }

        public RopeConnectable ConnectedItem => _connectable;
        public Candy ConnectedCandy => _connectable as Candy;
        public Transform AnchorPoint => _anchorPoint;
    }
}

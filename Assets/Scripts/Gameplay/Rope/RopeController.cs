using UnityEngine;
using System.Collections.Generic;
using Gameplay.Collectible;
using Systems;
using UnityEngine.Serialization;

namespace Gameplay.Rope
{
    public enum RopeVisualStyle
    {
        Straight,
        Spring
    }

    /// <summary>
    /// 绳子控制器，管理物理节点链 + LineRenderer 视觉
    /// 绳子由一串 Rigidbody2D 节点通过 DistanceJoint2D 串联，
    /// LineRenderer 追踪节点位置并用 Sprite 纹理渲染出连续绳子
    /// 切割时销毁目标关节，物理系统自然拆分两端
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeController : MonoBehaviour
    {
        public enum RopeConnectionMode
        {
            AnchorToConnectable,
            AnchorToAnchor
        }

        [SerializeField] private RopeConnectionMode _connectionMode = RopeConnectionMode.AnchorToConnectable;

        [FormerlySerializedAs("_anchorPoint")]
        [SerializeField] private Transform _startAnchorPoint;

        [SerializeField] private Transform _endAnchorPoint;

        [FormerlySerializedAs("_candy")]
        [SerializeField] private RopeConnectable _connectable;
        [SerializeField] private Transform _connectableAttachPoint;
        [FormerlySerializedAs("_connectedEnd")]
        [SerializeField, HideInInspector] private Rigidbody2D _legacyConnectedEndRigidbody;

        [SerializeField] private int _nodeCount = 15;
        [SerializeField] private float _nodeMass = 0.05f;
        [SerializeField] private float _nodeRadius = 0.15f;
        [SerializeField] private float _ropeWidth = 0.08f;

        [Header("Spring Mode")]
        [SerializeField] private bool _useSpringJoints;
        [SerializeField, Range(0.1f, 2f)] private float _springRestLengthRatio = 1f;
        [SerializeField] private float _springFrequency = 5f;
        [SerializeField] private float _springDampingRatio = 0.3f;

        [Header("Visual Shape")]
        [SerializeField] private RopeVisualStyle _visualStyle = RopeVisualStyle.Straight;
        [SerializeField, Min(1)] private int _springTurnsPerSegment = 1;
        [SerializeField, Min(2)] private int _springSamplesPerTurn = 8;
        [SerializeField, Min(0f)] private float _springAmplitude = 0.12f;

        [Header("Cut Fade")]
        [SerializeField, Min(0f)] private float _cutFadeDelay = 0.15f;
        [SerializeField, Min(0.01f)] private float _cutFadeDuration = 0.4f;

        [Header("Protection")]
        [SerializeField] private bool _indestructible;
        [SerializeField] private bool _disableNodeColliders;

        private LineRenderer _lineRenderer;
        private readonly List<RopeChain> _chains = new List<RopeChain>();

        public event System.Action<RopeController> OnCut;

        private class RopeChain
        {
            public Transform StartAnchorPoint;
            public Transform EndAnchorPoint;
            public bool ConnectsToConnectable;
            public Vector2 ConnectableLocalAttachPoint;
            public LineRenderer LineRenderer;
            public Gradient OriginalGradient;
            public AnchoredJoint2D StartAnchorJoint;
            public AnchoredJoint2D EndJoint;
            public bool UsesWorldConnectableAnchor;
            public readonly List<GameObject> Nodes = new List<GameObject>();
            public bool IsCut;
            public bool IsFadingOut;
            public bool FadeComplete;
            public float FadeTimer;
        }

        protected virtual void Awake()
        {
            ApplyLegacyEndpointFallback();
            _lineRenderer = GetComponent<LineRenderer>();
        }

        protected virtual void Start()
        {
            CreateRopeChains();
        }

        public void ConfigureAnchorToConnectable(
            Transform startAnchorPoint,
            RopeConnectable connectable,
            Transform connectableAttachPoint = null)
        {
            _connectionMode = RopeConnectionMode.AnchorToConnectable;
            _startAnchorPoint = startAnchorPoint;
            _connectable = connectable;
            _connectableAttachPoint = connectableAttachPoint;
            _endAnchorPoint = null;
            _legacyConnectedEndRigidbody = null;
        }

        public void ConfigureAnchorToAnchor(Transform startAnchorPoint, Transform endAnchorPoint)
        {
            _connectionMode = RopeConnectionMode.AnchorToAnchor;
            _startAnchorPoint = startAnchorPoint;
            _endAnchorPoint = endAnchorPoint;
            _connectable = null;
            _connectableAttachPoint = null;
            _legacyConnectedEndRigidbody = null;
        }

        public void SetProtection(bool indestructible, bool disableNodeColliders)
        {
            _indestructible = indestructible;
            _disableNodeColliders = disableNodeColliders;
        }

        /// <summary>
        /// 创建物理节点链：AnchorToConnectable 为锚点 → 节点 → 可连接物；
        /// AnchorToAnchor 为起始锚点 → 节点 → 结束锚点。
        /// </summary>
        private void CreateRopeChains()
        {
            if (_nodeCount <= 0) return;

            RopeConnectionMode mode = ResolveConnectionMode();
            if (mode == RopeConnectionMode.AnchorToConnectable)
            {
                CreateAnchorToConnectableChain();
                return;
            }

            CreateAnchorToAnchorChain();
        }

        private void CreateAnchorToConnectableChain()
        {
            if (_startAnchorPoint == null || _connectable == null) return;

            _connectable.ReleaseInitialConnection();
            Vector2 localAttachPoint = _connectableAttachPoint != null
                ? _connectable.transform.InverseTransformPoint(_connectableAttachPoint.position)
                : _connectable.GetLocalAttachPoint(_startAnchorPoint.position);

            RopeChain chain = new RopeChain
            {
                StartAnchorPoint = _startAnchorPoint,
                ConnectsToConnectable = true,
                ConnectableLocalAttachPoint = localAttachPoint,
                LineRenderer = CreateLineRenderer(0)
            };

            CreateRopeChain(chain, _connectable.Rigidbody, null, 0);
            SetupLineRenderer(chain.LineRenderer);
            chain.OriginalGradient = RopeLineFade.CloneGradient(chain.LineRenderer.colorGradient);
            _chains.Add(chain);
            _connectable.RegisterRope();
        }

        private void CreateAnchorToAnchorChain()
        {
            if (_startAnchorPoint == null || _endAnchorPoint == null || _startAnchorPoint == _endAnchorPoint)
                return;

            RopeChain chain = new RopeChain
            {
                StartAnchorPoint = _startAnchorPoint,
                EndAnchorPoint = _endAnchorPoint,
                ConnectsToConnectable = false,
                LineRenderer = CreateLineRenderer(0)
            };

            Rigidbody2D endAnchorRb = EnsureKinematicBody(_endAnchorPoint);
            CreateRopeChain(chain, null, endAnchorRb, 0);
            SetupLineRenderer(chain.LineRenderer);
            chain.OriginalGradient = RopeLineFade.CloneGradient(chain.LineRenderer.colorGradient);
            _chains.Add(chain);
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

        private void CreateRopeChain(RopeChain chain, Rigidbody2D connectableRb, Rigidbody2D endAnchorRb, int chainIndex)
        {
            EnsureKinematicBody(chain.StartAnchorPoint);

            Vector2 anchorPos = chain.StartAnchorPoint.position;
            Vector2 endPos = GetEndAttachWorldPoint(chain);
            float segmentDistance = Vector2.Distance(anchorPos, endPos) / (_nodeCount + 1);

            // 创建所有节点（物理组件 + 碰撞体，不含 SpriteRenderer）
            for (int i = 0; i < _nodeCount; i++)
            {
                float t = (i + 1) / (float)(_nodeCount + 1);
                Vector2 pos = Vector2.Lerp(anchorPos, endPos, t);

                GameObject node = new GameObject("RopeNode_" + chainIndex + "_" + i);
                node.transform.position = pos;
                node.transform.SetParent(transform);

                Rigidbody2D rb = node.AddComponent<Rigidbody2D>();
                rb.mass = _nodeMass;
                rb.drag = 0.5f;
                rb.gravityScale = 1f;

                if (!_disableNodeColliders)
                {
                    CircleCollider2D col = node.AddComponent<CircleCollider2D>();
                    col.radius = _nodeRadius;
                }

                chain.Nodes.Add(node);

                // 忽略相邻节点碰撞
                if (!_disableNodeColliders && i > 0)
                {
                    Physics2D.IgnoreCollision(
                        node.GetComponent<CircleCollider2D>(),
                        chain.Nodes[i - 1].GetComponent<CircleCollider2D>()
                    );
                }
            }

            // 设置关节连接
            chain.StartAnchorJoint = CreateJoint(chain.StartAnchorPoint.gameObject, chain.Nodes[0].GetComponent<Rigidbody2D>(), segmentDistance);

            for (int i = 0; i < _nodeCount; i++)
            {
                AnchoredJoint2D joint = CreateJoint(chain.Nodes[i]);

                if (i < _nodeCount - 1)
                {
                    joint.connectedBody = chain.Nodes[i + 1].GetComponent<Rigidbody2D>();
                }
                else
                {
                    if (chain.ConnectsToConnectable)
                    {
                        if (connectableRb != null)
                        {
                            joint.connectedBody = connectableRb;
                            joint.connectedAnchor = chain.ConnectableLocalAttachPoint;
                        }
                        else
                        {
                            joint.connectedBody = null;
                            joint.connectedAnchor = GetEndAttachWorldPoint(chain);
                            chain.UsesWorldConnectableAnchor = true;
                        }
                    }
                    else
                    {
                        joint.connectedBody = endAnchorRb;
                        joint.connectedAnchor = Vector2.zero;
                    }

                    joint.autoConfigureConnectedAnchor = false;
                    chain.EndJoint = joint;
                }

                ConfigureJoint(joint, segmentDistance);
            }
        }

        private AnchoredJoint2D CreateJoint(GameObject owner, Rigidbody2D connectedBody, float distance)
        {
            if (_useSpringJoints)
            {
                SpringJoint2D sj = owner.AddComponent<SpringJoint2D>();
                sj.connectedBody = connectedBody;
                sj.autoConfigureDistance = false;
                sj.distance = distance * _springRestLengthRatio;
                sj.frequency = _springFrequency;
                sj.dampingRatio = _springDampingRatio;
                return sj;
            }

            DistanceJoint2D dj = owner.AddComponent<DistanceJoint2D>();
            dj.connectedBody = connectedBody;
            dj.autoConfigureDistance = false;
            dj.distance = distance;
            return dj;
        }

        private AnchoredJoint2D CreateJoint(GameObject owner)
        {
            if (_useSpringJoints)
            {
                SpringJoint2D sj = owner.AddComponent<SpringJoint2D>();
                sj.autoConfigureDistance = false;
                sj.distance = 0f;
                sj.frequency = _springFrequency;
                sj.dampingRatio = _springDampingRatio;
                return sj;
            }

            DistanceJoint2D dj = owner.AddComponent<DistanceJoint2D>();
            dj.autoConfigureDistance = false;
            dj.distance = 0f;
            return dj;
        }

        private void ConfigureJoint(AnchoredJoint2D joint, float distance)
        {
            if (joint is DistanceJoint2D dj)
            {
                dj.autoConfigureDistance = false;
                dj.distance = distance;
            }
            else if (joint is SpringJoint2D sj)
            {
                sj.autoConfigureDistance = false;
                sj.distance = distance * _springRestLengthRatio;
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
            lineRenderer.textureMode = LineTextureMode.Stretch;
        }

        protected virtual void Update()
        {
            if (_chains.Count == 0) return;

            if (ShouldDestroyBecauseEndpointMissing())
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

            Vector3[] positions = GetVisualRopePositions(chain, !chain.IsCut);
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
            if (!chain.ConnectsToConnectable || !chain.UsesWorldConnectableAnchor || chain.IsCut || chain.EndJoint == null)
                return;

            chain.EndJoint.connectedAnchor = GetEndAttachWorldPoint(chain);
        }

        /// <summary>
        /// 获取绳子所有节点位置，用于切割检测
        /// </summary>
        public Vector3[] GetRopePositions()
        {
            if (_chains.Count == 0)
                return new Vector3[0];

            return GetVisualRopePositions(_chains[0], !_chains[0].IsCut);
        }

        private Vector3[] GetPhysicsRopePositions(RopeChain chain, bool includeEndPoint)
        {
            int pointCount = chain.Nodes.Count + 1 + (includeEndPoint ? 1 : 0);
            Vector3[] positions = new Vector3[pointCount];

            positions[0] = chain.StartAnchorPoint.position;
            for (int i = 0; i < chain.Nodes.Count; i++)
                positions[i + 1] = chain.Nodes[i].transform.position;

            if (includeEndPoint)
                positions[pointCount - 1] = GetEndAttachWorldPoint(chain);

            return positions;
        }

        private Vector3[] GetVisualRopePositions(RopeChain chain, bool includeEndPoint)
        {
            return RopeLineShape.BuildPositions(
                GetPhysicsRopePositions(chain, includeEndPoint),
                _visualStyle,
                _springTurnsPerSegment,
                _springSamplesPerTurn,
                _springAmplitude);
        }

        private RopeVisualSample[] GetCuttableRopeSamples(RopeChain chain, bool includeEndPoint)
        {
            return RopeLineShape.BuildSamples(
                GetPhysicsRopePositions(chain, includeEndPoint),
                _visualStyle,
                _springTurnsPerSegment,
                _springSamplesPerTurn,
                _springAmplitude);
        }

        private Vector3 GetEndAttachWorldPoint(RopeChain chain)
        {
            if (chain.ConnectsToConnectable)
            {
                if (_connectable == null)
                    return Vector3.zero;

                return _connectable.GetWorldAttachPoint(chain.ConnectableLocalAttachPoint);
            }

            return chain.EndAnchorPoint != null ? chain.EndAnchorPoint.position : Vector3.zero;
        }

        public bool TryCut(Vector2 lineStart, Vector2 lineEnd)
        {
            if (_indestructible) return false;

            for (int chainIndex = 0; chainIndex < _chains.Count; chainIndex++)
            {
                RopeChain chain = _chains[chainIndex];
                if (chain.IsCut) continue;

                RopeVisualSample[] ropeSamples = GetCuttableRopeSamples(chain, true);
                for (int visualSegmentIndex = 0; visualSegmentIndex < ropeSamples.Length - 1; visualSegmentIndex++)
                {
                    Vector2 ropeSegStart = ropeSamples[visualSegmentIndex].Position;
                    Vector2 ropeSegEnd = ropeSamples[visualSegmentIndex + 1].Position;

                    if (SegmentsIntersect(lineStart, lineEnd, ropeSegStart, ropeSegEnd))
                    {
                        Vector2 hitPoint = GetIntersectionPoint(lineStart, lineEnd, ropeSegStart, ropeSegEnd);
                        int segmentIndex = ropeSamples[visualSegmentIndex + 1].SegmentIndex;
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

            CutChain(chain, anchorPoint.position, GetDropSegmentIndex(chain, anchorPoint));
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
            if (chain.IsCut || chain.StartAnchorPoint == null) return;
            if (chain.ConnectsToConnectable && _connectable == null) return;

            int nodeCount = chain.Nodes.Count;
            if (segmentIndex < 0 || segmentIndex > nodeCount) return;

            chain.IsCut = true;
            SfxPlayer.Play(SfxId.Slice);

            if (_useSpringJoints)
                SfxPlayer.Play(SfxId.Spring);

            // segmentIndex 0 是起始锚点到第一个节点；之后是节点之间；最后一段是末节点到末端对象。
            AnchoredJoint2D jointToDestroy = segmentIndex == 0
                ? chain.StartAnchorJoint
                : chain.Nodes[segmentIndex - 1].GetComponent<AnchoredJoint2D>();
            if (jointToDestroy != null)
            {
                Destroy(jointToDestroy);
            }

            if (chain.ConnectsToConnectable)
                _connectable.NotifyRopeCut(hitPoint);

            NotifyAnchorsCut(chain, hitPoint);
            OnRopeCut(hitPoint, GetRemainingUncutChainCount());
            OnCut?.Invoke(this);

            // 下半段节点构建为 RopeSegment（含 LineRenderer）
            List<GameObject> lowerNodes = new List<GameObject>();
            for (int i = segmentIndex; i < chain.Nodes.Count; i++)
            {
                lowerNodes.Add(chain.Nodes[i]);
            }

            bool keepLowerSegmentConnectedToEnd = ShouldKeepLowerSegmentConnectedToEnd(chain);

            if (!keepLowerSegmentConnectedToEnd && chain.EndJoint != null)
            {
                Destroy(chain.EndJoint);
                chain.EndJoint = null;
            }

            if (lowerNodes.Count > 0)
            {
                GameObject lowerGO = new GameObject(gameObject.name + "_下半段");
                lowerGO.transform.SetParent(transform);

                RopeSegment lowerSegment = lowerGO.AddComponent<RopeSegment>();
                lowerSegment.Initialize(
                    lowerNodes,
                    _ropeWidth,
                    _visualStyle,
                    _springTurnsPerSegment,
                    _springSamplesPerTurn,
                    _springAmplitude,
                    chain.LineRenderer.sharedMaterial,
                    chain.OriginalGradient,
                    _cutFadeDelay,
                    _cutFadeDuration,
                    keepLowerSegmentConnectedToEnd ? GetEndTransform(chain) : null,
                    chain.ConnectableLocalAttachPoint);

                // 从上半段列表中移除下半段节点
                chain.Nodes.RemoveRange(segmentIndex, lowerNodes.Count);
            }

            BeginChainFade(chain);
        }

        protected virtual void OnRopeCut(Vector3 hitPoint, int remainingUncutChainCount)
        {
        }

        private int GetRemainingUncutChainCount()
        {
            int count = 0;
            for (int i = 0; i < _chains.Count; i++)
            {
                if (!_chains[i].IsCut)
                    count++;
            }

            return count;
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

            if (chain.StartAnchorJoint != null)
            {
                Destroy(chain.StartAnchorJoint);
                chain.StartAnchorJoint = null;
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
                if (_chains[i].StartAnchorPoint == anchorPoint || _chains[i].EndAnchorPoint == anchorPoint)
                    return _chains[i];
            }

            return null;
        }

        private int GetDropSegmentIndex(RopeChain chain, Transform anchorPoint)
        {
            if (chain.EndAnchorPoint == anchorPoint)
                return chain.Nodes.Count;

            return 0;
        }

        private void NotifyAnchorsCut(RopeChain chain, Vector3 hitPoint)
        {
            NotifyAnchorCut(chain.StartAnchorPoint, hitPoint);

            if (!chain.ConnectsToConnectable && chain.EndAnchorPoint != chain.StartAnchorPoint)
                NotifyAnchorCut(chain.EndAnchorPoint, hitPoint);
        }

        private void NotifyAnchorCut(Transform anchorPoint, Vector3 hitPoint)
        {
            if (anchorPoint == null) return;

            RopeAnchor anchor = anchorPoint.GetComponent<RopeAnchor>();
            if (anchor != null)
                anchor.OnRopeCut(this, anchorPoint, hitPoint);
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

        protected virtual void OnDestroy()
        {
            for (int chainIndex = _chains.Count - 1; chainIndex >= 0; chainIndex--)
            {
                RopeChain chain = _chains[chainIndex];
                if (chain.StartAnchorJoint != null)
                {
                    Destroy(chain.StartAnchorJoint);
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
        public RopeConnectionMode ConnectionMode => ResolveConnectionMode();
        public RopeVisualStyle VisualStyle => _visualStyle;
        public Transform AnchorPoint => _startAnchorPoint;
        public Transform StartAnchorPoint => _startAnchorPoint;
        public Transform EndAnchorPoint => _endAnchorPoint;

        private RopeConnectionMode ResolveConnectionMode()
        {
            if (_connectionMode == RopeConnectionMode.AnchorToConnectable && _connectable == null && _endAnchorPoint != null)
                return RopeConnectionMode.AnchorToAnchor;

            return _connectionMode;
        }

        private bool ShouldDestroyBecauseEndpointMissing()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                RopeChain chain = _chains[i];
                if (chain.StartAnchorPoint == null)
                    return true;
                if (chain.ConnectsToConnectable && _connectable == null)
                    return true;
                if (!chain.ConnectsToConnectable && chain.EndAnchorPoint == null)
                    return true;
            }

            return false;
        }

        private Rigidbody2D EnsureKinematicBody(Transform anchorPoint)
        {
            if (anchorPoint == null) return null;

            Rigidbody2D anchorRb = anchorPoint.GetComponent<Rigidbody2D>();
            if (anchorRb == null)
            {
                anchorRb = anchorPoint.gameObject.AddComponent<Rigidbody2D>();
            }

            anchorRb.bodyType = RigidbodyType2D.Kinematic;
            return anchorRb;
        }

        private bool ShouldKeepLowerSegmentConnectedToEnd(RopeChain chain)
        {
            if (!chain.ConnectsToConnectable)
                return chain.EndAnchorPoint != null;

            return _connectable != null && _connectable.Rigidbody != null;
        }

        private Transform GetEndTransform(RopeChain chain)
        {
            return chain.ConnectsToConnectable
                ? (_connectable != null ? _connectable.transform : null)
                : chain.EndAnchorPoint;
        }

        private void ApplyLegacyEndpointFallback()
        {
            if (_connectable == null && _legacyConnectedEndRigidbody != null)
                _connectable = _legacyConnectedEndRigidbody.GetComponent<RopeConnectable>();

            if (_endAnchorPoint == null && _legacyConnectedEndRigidbody != null)
                _endAnchorPoint = _legacyConnectedEndRigidbody.transform;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using Gameplay.Collectible;

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
        [SerializeField] private Candy _candy;

        [SerializeField] private int _nodeCount = 15;
        [SerializeField] private float _nodeMass = 0.05f;
        [SerializeField] private float _nodeRadius = 0.15f;
        [SerializeField] private float _ropeWidth = 0.08f;
        [SerializeField] private float _sagFactor = 1.5f;

        private LineRenderer _lineRenderer;
        private List<GameObject> _nodes;
        private bool _isCut;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        private void Start()
        {
            CreateRopeChain();
            _candy.Release();
            SetupLineRenderer();
        }

        /// <summary>
        /// 创建物理节点链：锚点 → 节点[0..N-1] → 糖果
        /// </summary>
        private void CreateRopeChain()
        {
            if (_nodeCount <= 0) return;

            _nodes = new List<GameObject>();

            // 确保锚点有 Kinematic Rigidbody2D（关节需要）
            Rigidbody2D anchorRb = _anchorPoint.GetComponent<Rigidbody2D>();
            if (anchorRb == null)
            {
                anchorRb = _anchorPoint.gameObject.AddComponent<Rigidbody2D>();
            }
            anchorRb.bodyType = RigidbodyType2D.Kinematic;

            Vector2 anchorPos = _anchorPoint.position;
            Vector2 candyPos = _candy.transform.position;
            Rigidbody2D candyRb = _candy.GetComponent<Rigidbody2D>();

            // 创建所有节点（物理组件 + 碰撞体，不含 SpriteRenderer）
            for (int i = 0; i < _nodeCount; i++)
            {
                float t = (i + 1) / (float)(_nodeCount + 1);
                Vector2 pos = Vector2.Lerp(anchorPos, candyPos, t);
                float sag = Mathf.Sin(t * Mathf.PI) * _sagFactor;
                pos += Vector2.down * sag;

                GameObject node = new GameObject("RopeNode_" + i);
                node.transform.position = pos;
                node.transform.SetParent(transform);

                Rigidbody2D rb = node.AddComponent<Rigidbody2D>();
                rb.mass = _nodeMass;
                rb.drag = 0.5f;
                rb.gravityScale = 1f;

                CircleCollider2D col = node.AddComponent<CircleCollider2D>();
                col.radius = _nodeRadius;

                _nodes.Add(node);

                // 忽略相邻节点碰撞
                if (i > 0)
                {
                    Physics2D.IgnoreCollision(
                        node.GetComponent<CircleCollider2D>(),
                        _nodes[i - 1].GetComponent<CircleCollider2D>()
                    );
                }
            }

            // 设置关节连接
            DistanceJoint2D anchorJoint = _anchorPoint.gameObject.AddComponent<DistanceJoint2D>();
            anchorJoint.connectedBody = _nodes[0].GetComponent<Rigidbody2D>();
            anchorJoint.autoConfigureDistance = true;

            for (int i = 0; i < _nodeCount; i++)
            {
                DistanceJoint2D joint = _nodes[i].AddComponent<DistanceJoint2D>();
                joint.autoConfigureDistance = true;

                if (i < _nodeCount - 1)
                {
                    joint.connectedBody = _nodes[i + 1].GetComponent<Rigidbody2D>();
                }
                else
                {
                    joint.connectedBody = candyRb;
                }
            }
        }

        /// <summary>
        /// 配置 LineRenderer 宽度与纹理模式
        /// </summary>
        private void SetupLineRenderer()
        {
            _lineRenderer.positionCount = _nodes.Count + 2;
            _lineRenderer.startWidth = _ropeWidth;
            _lineRenderer.endWidth = _ropeWidth;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.textureMode = LineTextureMode.Tile;
        }

        private void Update()
        {
            if (_nodes == null || _nodes.Count == 0) return;

            // 未切割：锚点 → 节点链 → 糖果
            // 已切割：锚点 → 上半段节点链（糖果由 RopeSegment 渲染）
            int nodeCount = _nodes.Count;
            int extraPoints = _isCut ? 1 : 2;
            int pointCount = nodeCount + extraPoints;

            if (_lineRenderer.positionCount != pointCount)
                _lineRenderer.positionCount = pointCount;

            _lineRenderer.SetPosition(0, _anchorPoint.position);
            for (int i = 0; i < nodeCount; i++)
                _lineRenderer.SetPosition(i + 1, _nodes[i].transform.position);

            if (!_isCut)
                _lineRenderer.SetPosition(pointCount - 1, _candy.transform.position);
        }

        /// <summary>
        /// 获取绳子所有节点位置，用于切割检测
        /// </summary>
        public Vector3[] GetRopePositions()
        {
            Vector3[] positions = new Vector3[_nodes.Count];
            for (int i = 0; i < _nodes.Count; i++)
            {
                positions[i] = _nodes[i].transform.position;
            }
            return positions;
        }

        /// <summary>
        /// 切割绳子，在切割点拆分为两段
        /// </summary>
        /// <param name="segmentIndex">切割命中的线段索引（Node[i] 与 Node[i+1] 之间的线段）</param>
        public void Cut(Vector3 hitPoint, int segmentIndex)
        {
            if (_isCut || _candy == null || _anchorPoint == null) return;
            if (segmentIndex < 0 || segmentIndex >= _nodes.Count - 1) return;

            _isCut = true;

            // 销毁节点 segmentIndex 上的关节（连接至 segmentIndex+1）
            DistanceJoint2D jointToDestroy = _nodes[segmentIndex].GetComponent<DistanceJoint2D>();
            if (jointToDestroy != null)
            {
                Destroy(jointToDestroy);
            }

            // 下半段节点构建为 RopeSegment（含 LineRenderer）
            int lowerCount = _nodes.Count - segmentIndex - 1;
            List<GameObject> lowerNodes = new List<GameObject>();
            for (int i = segmentIndex + 1; i < _nodes.Count; i++)
            {
                lowerNodes.Add(_nodes[i]);
            }

            GameObject lowerGO = new GameObject(gameObject.name + "_下半段");
            lowerGO.transform.SetParent(transform);

            RopeSegment lowerSegment = lowerGO.AddComponent<RopeSegment>();
            lowerSegment.Initialize(lowerNodes, _ropeWidth, _lineRenderer.material, _candy.transform);

            // 从上半段列表中移除下半段节点
            _nodes.RemoveRange(segmentIndex + 1, lowerCount);
        }

        private void OnDestroy()
        {
            if (_nodes == null) return;
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i] != null)
                {
                    Destroy(_nodes[i]);
                }
            }
        }

        public bool IsCut => _isCut;
        public Candy ConnectedCandy => _candy;
        public Transform AnchorPoint => _anchorPoint;
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace Gameplay.Rope
{
    /// <summary>
    /// 绳子片段，切割后下半段的物理节点链
    /// 节点已由 RopeController 创建好，此处仅接管生命周期并用 LineRenderer 渲染
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeSegment : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private List<GameObject> _nodes;
        private Transform _candyTransform;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        /// <summary>
        /// 接管切割后的下半段节点，配置 LineRenderer
        /// </summary>
        public void Initialize(List<GameObject> lowerNodes, float ropeWidth, Material ropeMaterial, Transform candyTransform)
        {
            _nodes = lowerNodes;
            _candyTransform = candyTransform;

            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].transform.SetParent(transform);
            }

            _lineRenderer.positionCount = _nodes.Count + 1;
            _lineRenderer.startWidth = ropeWidth;
            _lineRenderer.endWidth = ropeWidth;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.textureMode = LineTextureMode.Tile;
            _lineRenderer.material = ropeMaterial;
        }

        private void Update()
        {
            if (_nodes == null || _nodes.Count == 0)
            {
                Destroy(gameObject);
                return;
            }

            // 节点全被销毁时自清理
            bool anyAlive = false;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null)
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive)
            {
                Destroy(gameObject);
                return;
            }

            // 节点链 → 糖果，包含完整下端连线
            int pointCount = _nodes.Count + 1;
            if (_lineRenderer.positionCount != pointCount)
                _lineRenderer.positionCount = pointCount;

            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null)
                    _lineRenderer.SetPosition(i, _nodes[i].transform.position);
            }

            if (_candyTransform != null)
                _lineRenderer.SetPosition(pointCount - 1, _candyTransform.position);
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
    }
}

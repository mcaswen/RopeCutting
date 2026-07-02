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
        private Transform _connectableTransform;
        private Vector2 _connectableLocalAttachPoint;
        private Gradient _baseColorGradient;
        private float _fadeDelay;
        private float _fadeDuration = 0.4f;
        private float _fadeTimer;
        private bool _hasConnectable;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        /// <summary>
        /// 接管切割后的下半段节点，配置 LineRenderer
        /// </summary>
        public void Initialize(
            List<GameObject> lowerNodes,
            float ropeWidth,
            Material ropeMaterial,
            Gradient ropeColorGradient,
            float fadeDelay,
            float fadeDuration,
            Transform connectableTransform,
            Vector2 connectableLocalAttachPoint = default)
        {
            _nodes = lowerNodes;
            _connectableTransform = connectableTransform;
            _connectableLocalAttachPoint = connectableLocalAttachPoint;
            _hasConnectable = connectableTransform != null;
            _baseColorGradient = RopeLineFade.CloneGradient(ropeColorGradient);
            _fadeDelay = Mathf.Max(0f, fadeDelay);
            _fadeDuration = Mathf.Max(0.01f, fadeDuration);
            _fadeTimer = 0f;

            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].transform.SetParent(transform);
            }

            _lineRenderer.positionCount = GetPointCount();
            _lineRenderer.startWidth = ropeWidth;
            _lineRenderer.endWidth = ropeWidth;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.textureMode = LineTextureMode.Tile;
            _lineRenderer.material = ropeMaterial;
            _lineRenderer.colorGradient = RopeLineFade.CloneGradient(_baseColorGradient);
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

            // 可连接物被销毁时立即清理
            if (_hasConnectable && _connectableTransform == null)
            {
                Destroy(gameObject);
                return;
            }

            // 节点链 → 可连接物，包含完整下端连线
            int pointCount = GetPointCount();
            if (_lineRenderer.positionCount != pointCount)
                _lineRenderer.positionCount = pointCount;

            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null)
                    _lineRenderer.SetPosition(i, _nodes[i].transform.position);
            }

            if (_connectableTransform != null)
                _lineRenderer.SetPosition(pointCount - 1, _connectableTransform.TransformPoint(_connectableLocalAttachPoint));

            UpdateFade();
        }

        private void UpdateFade()
        {
            _fadeTimer += Time.deltaTime;

            float fadeElapsed = Mathf.Max(0f, _fadeTimer - _fadeDelay);
            float alphaMultiplier = 1f - Mathf.Clamp01(fadeElapsed / Mathf.Max(0.01f, _fadeDuration));
            RopeLineFade.ApplyAlpha(_lineRenderer, _baseColorGradient, alphaMultiplier);

            if (fadeElapsed >= _fadeDuration)
                Destroy(gameObject);
        }

        private int GetPointCount()
        {
            int nodeCount = _nodes != null ? _nodes.Count : 0;
            return nodeCount + (_connectableTransform != null ? 1 : 0);
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

    internal static class RopeLineFade
    {
        public static Gradient CloneGradient(Gradient source)
        {
            Gradient gradient = new Gradient();
            if (source == null)
                return gradient;

            gradient.SetKeys(source.colorKeys, source.alphaKeys);
            gradient.mode = source.mode;
            return gradient;
        }

        public static void ApplyAlpha(LineRenderer lineRenderer, Gradient baseGradient, float alphaMultiplier)
        {
            if (lineRenderer == null || baseGradient == null) return;

            alphaMultiplier = Mathf.Clamp01(alphaMultiplier);
            Gradient fadeGradient = new Gradient();
            GradientAlphaKey[] baseAlphaKeys = baseGradient.alphaKeys;
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[baseAlphaKeys.Length];

            for (int i = 0; i < baseAlphaKeys.Length; i++)
            {
                alphaKeys[i] = new GradientAlphaKey(
                    baseAlphaKeys[i].alpha * alphaMultiplier,
                    baseAlphaKeys[i].time);
            }

            fadeGradient.SetKeys(baseGradient.colorKeys, alphaKeys);
            fadeGradient.mode = baseGradient.mode;
            lineRenderer.colorGradient = fadeGradient;
        }
    }
}

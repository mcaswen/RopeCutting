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
        private RopeVisualStyle _visualStyle = RopeVisualStyle.Straight;
        private int _springTurnsPerSegment = 1;
        private int _springSamplesPerTurn = 8;
        private float _springAmplitude = 0.12f;
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
            RopeVisualStyle visualStyle,
            int springTurnsPerSegment,
            int springSamplesPerTurn,
            float springAmplitude,
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
            _visualStyle = visualStyle;
            _springTurnsPerSegment = Mathf.Max(1, springTurnsPerSegment);
            _springSamplesPerTurn = Mathf.Max(2, springSamplesPerTurn);
            _springAmplitude = Mathf.Max(0f, springAmplitude);
            _baseColorGradient = RopeLineFade.CloneGradient(ropeColorGradient);
            _fadeDelay = Mathf.Max(0f, fadeDelay);
            _fadeDuration = Mathf.Max(0.01f, fadeDuration);
            _fadeTimer = 0f;

            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].transform.SetParent(transform);
            }

            _lineRenderer.positionCount = 0;
            _lineRenderer.startWidth = ropeWidth;
            _lineRenderer.endWidth = ropeWidth;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.textureMode = LineTextureMode.Stretch;
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
            Vector3[] visualPositions = RopeLineShape.BuildPositions(
                GetCenterlinePositions(),
                _visualStyle,
                _springTurnsPerSegment,
                _springSamplesPerTurn,
                _springAmplitude);

            if (_lineRenderer.positionCount != visualPositions.Length)
                _lineRenderer.positionCount = visualPositions.Length;

            for (int i = 0; i < visualPositions.Length; i++)
                _lineRenderer.SetPosition(i, visualPositions[i]);

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

        private Vector3[] GetCenterlinePositions()
        {
            int pointCount = GetPointCount();
            Vector3[] positions = new Vector3[pointCount];

            int writeIndex = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] == null) continue;

                positions[writeIndex] = _nodes[i].transform.position;
                writeIndex++;
            }

            if (_connectableTransform != null)
            {
                positions[writeIndex] = _connectableTransform.TransformPoint(_connectableLocalAttachPoint);
                writeIndex++;
            }

            if (writeIndex == positions.Length)
                return positions;

            Vector3[] compactPositions = new Vector3[writeIndex];
            for (int i = 0; i < writeIndex; i++)
                compactPositions[i] = positions[i];

            return compactPositions;
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

    internal readonly struct RopeVisualSample
    {
        public RopeVisualSample(Vector3 position, int segmentIndex)
        {
            Position = position;
            SegmentIndex = segmentIndex;
        }

        public readonly Vector3 Position;
        public readonly int SegmentIndex;
    }

    internal static class RopeLineShape
    {
        public static Vector3[] BuildPositions(
            IReadOnlyList<Vector3> centerlinePositions,
            RopeVisualStyle visualStyle,
            int springTurnsPerSegment,
            int springSamplesPerTurn,
            float springAmplitude)
        {
            RopeVisualSample[] samples = BuildSamples(
                centerlinePositions,
                visualStyle,
                springTurnsPerSegment,
                springSamplesPerTurn,
                springAmplitude);

            Vector3[] positions = new Vector3[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                positions[i] = samples[i].Position;

            return positions;
        }

        public static RopeVisualSample[] BuildSamples(
            IReadOnlyList<Vector3> centerlinePositions,
            RopeVisualStyle visualStyle,
            int springTurnsPerSegment,
            int springSamplesPerTurn,
            float springAmplitude)
        {
            if (centerlinePositions == null || centerlinePositions.Count == 0)
                return new RopeVisualSample[0];

            if (centerlinePositions.Count == 1 || visualStyle == RopeVisualStyle.Straight || springAmplitude <= 0f)
                return BuildStraightSamples(centerlinePositions);

            return BuildSpringSamples(
                centerlinePositions,
                Mathf.Max(1, springTurnsPerSegment),
                Mathf.Max(2, springSamplesPerTurn),
                Mathf.Max(0f, springAmplitude));
        }

        private static RopeVisualSample[] BuildStraightSamples(IReadOnlyList<Vector3> centerlinePositions)
        {
            RopeVisualSample[] samples = new RopeVisualSample[centerlinePositions.Count];
            int lastSegmentIndex = Mathf.Max(0, centerlinePositions.Count - 2);

            for (int i = 0; i < centerlinePositions.Count; i++)
            {
                int segmentIndex = i == 0 ? 0 : Mathf.Min(i - 1, lastSegmentIndex);
                samples[i] = new RopeVisualSample(centerlinePositions[i], segmentIndex);
            }

            return samples;
        }

        private static RopeVisualSample[] BuildSpringSamples(
            IReadOnlyList<Vector3> centerlinePositions,
            int turnsPerSegment,
            int samplesPerTurn,
            float amplitude)
        {
            int samplesPerSegment = Mathf.Max(2, turnsPerSegment * samplesPerTurn);
            List<RopeVisualSample> samples = new List<RopeVisualSample>((centerlinePositions.Count - 1) * samplesPerSegment + 1);

            for (int segmentIndex = 0; segmentIndex < centerlinePositions.Count - 1; segmentIndex++)
            {
                Vector3 start = centerlinePositions[segmentIndex];
                Vector3 end = centerlinePositions[segmentIndex + 1];
                Vector3 segment = end - start;

                if (segment.sqrMagnitude <= Mathf.Epsilon)
                    continue;

                Vector3 normal = new Vector3(-segment.y, segment.x, 0f).normalized;

                if (samples.Count == 0)
                    samples.Add(new RopeVisualSample(start, segmentIndex));

                for (int sampleIndex = 1; sampleIndex <= samplesPerSegment; sampleIndex++)
                {
                    float t = sampleIndex / (float)samplesPerSegment;
                    float wave = Mathf.Sin(t * turnsPerSegment * Mathf.PI * 2f);
                    Vector3 position = Vector3.Lerp(start, end, t) + normal * (wave * amplitude);
                    samples.Add(new RopeVisualSample(position, segmentIndex));
                }
            }

            if (samples.Count == 0)
                return BuildStraightSamples(centerlinePositions);

            return samples.ToArray();
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

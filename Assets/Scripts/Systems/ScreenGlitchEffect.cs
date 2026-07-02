using System.Collections;
using UnityEngine;

namespace Systems
{
    [RequireComponent(typeof(Camera))]
    public class ScreenGlitchEffect : MonoBehaviour
    {
        [SerializeField] private Shader _shader;
        [SerializeField, Range(0f, 1f)] private float _intensity;

        [Header("Look")]
        [SerializeField, Range(0f, 0.05f)] private float _rgbSplit = 0.008f;
        [SerializeField, Range(0f, 0.08f)] private float _horizontalJitter = 0.025f;
        [SerializeField, Range(0f, 0.08f)] private float _leftRightShake = 0.012f;
        [SerializeField, Range(0f, 0.02f)] private float _waveDistortion = 0.004f;
        [SerializeField, Range(0f, 1f)] private float _blockChance = 0.35f;
        [SerializeField, Min(1f)] private float _blockFrequency = 38f;
        [SerializeField, Range(0f, 1f)] private float _scanlineStrength = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _noiseAmount = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _darken = 0.35f;

        [Header("Playback")]
        [SerializeField, Min(0.01f)] private float _defaultDuration = 1f;
        [SerializeField, Min(1f)] private float _jitterSpeed = 28f;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private bool _playOnEnable;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int RgbSplitId = Shader.PropertyToID("_RGBSplit");
        private static readonly int HorizontalJitterId = Shader.PropertyToID("_HorizontalJitter");
        private static readonly int LeftRightShakeId = Shader.PropertyToID("_LeftRightShake");
        private static readonly int WaveDistortionId = Shader.PropertyToID("_WaveDistortion");
        private static readonly int BlockChanceId = Shader.PropertyToID("_BlockChance");
        private static readonly int BlockFrequencyId = Shader.PropertyToID("_BlockFrequency");
        private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
        private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
        private static readonly int DarkenId = Shader.PropertyToID("_Darken");
        private static readonly int JitterSpeedId = Shader.PropertyToID("_JitterSpeed");
        private static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");

        private Material _material;
        private Coroutine _playRoutine;
        private float _currentIntensity;

        public float Intensity
        {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }

        public float CurrentIntensity => _currentIntensity;

        private void OnEnable()
        {
            EnsureMaterial();

            if (_playOnEnable && Application.isPlaying)
                Play();
        }

        private void OnDisable()
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            DestroyMaterial();
        }

        public void Play()
        {
            Play(_defaultDuration, _intensity);
        }

        public void Play(float duration)
        {
            Play(duration, _intensity);
        }

        public void Play(float duration, float strength)
        {
            if (_playRoutine != null)
                StopCoroutine(_playRoutine);

            _playRoutine = StartCoroutine(PlayRoutine(Mathf.Max(0.01f, duration), Mathf.Clamp01(strength)));
        }

        public void Stop()
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            _currentIntensity = 0f;
        }

        private IEnumerator PlayRoutine(float duration, float strength)
        {
            float elapsed = 0f;
            _currentIntensity = strength;

            while (elapsed < duration)
            {
                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _currentIntensity = Mathf.Lerp(strength, 0f, t * t * (3f - 2f * t));
                yield return null;
            }

            _currentIntensity = 0f;
            _playRoutine = null;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_currentIntensity <= 0f || !EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                return;
            }

            UpdateMaterial();
            Graphics.Blit(source, destination, _material);
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
                return true;

            if (_shader == null)
                _shader = Shader.Find("Hidden/Custom/ScreenGlitchDistortion");

            if (_shader == null || !_shader.isSupported)
                return false;

            _material = new Material(_shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            return true;
        }

        private void UpdateMaterial()
        {
            float time = _useUnscaledTime ? Time.unscaledTime : Time.time;

            _material.SetFloat(IntensityId, _currentIntensity);
            _material.SetFloat(RgbSplitId, _rgbSplit);
            _material.SetFloat(HorizontalJitterId, _horizontalJitter);
            _material.SetFloat(LeftRightShakeId, _leftRightShake);
            _material.SetFloat(WaveDistortionId, _waveDistortion);
            _material.SetFloat(BlockChanceId, _blockChance);
            _material.SetFloat(BlockFrequencyId, _blockFrequency);
            _material.SetFloat(ScanlineStrengthId, _scanlineStrength);
            _material.SetFloat(NoiseAmountId, _noiseAmount);
            _material.SetFloat(DarkenId, _darken);
            _material.SetFloat(JitterSpeedId, _jitterSpeed);
            _material.SetFloat(TimeSeedId, time);
        }

        private void DestroyMaterial()
        {
            if (_material == null) return;

            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);

            _material = null;
        }
    }
}

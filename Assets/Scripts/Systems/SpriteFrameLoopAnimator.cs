using UnityEngine;
using UnityEngine.UI;

namespace Systems
{
    [DisallowMultipleComponent]
    public sealed class SpriteFrameLoopAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField, Min(0.01f)] private float _framesPerSecond = 8f;
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private bool _loop = true;

        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private int _frameIndex;
        private float _timer;
        private bool _playing;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (_playOnEnable)
                Play();
            else
                ApplyFrame();
        }

        private void Update()
        {
            if (!_playing || _frames == null || _frames.Length == 0)
                return;

            float frameInterval = 1f / _framesPerSecond;
            _timer += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            while (_timer >= frameInterval)
            {
                _timer -= frameInterval;
                AdvanceFrame();
            }
        }

        public void Play()
        {
            if (_frames == null || _frames.Length == 0)
            {
                _playing = false;
                return;
            }

            _frameIndex = 0;
            _timer = 0f;
            _playing = true;
            ApplyFrame();
        }

        public void Stop()
        {
            _playing = false;
        }

        private void AdvanceFrame()
        {
            _frameIndex++;

            if (_frameIndex >= _frames.Length)
            {
                if (_loop)
                    _frameIndex = 0;
                else
                {
                    _frameIndex = _frames.Length - 1;
                    _playing = false;
                }
            }

            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_frames == null || _frames.Length == 0)
                return;

            Sprite sprite = _frames[Mathf.Clamp(_frameIndex, 0, _frames.Length - 1)];

            if (_image != null)
                _image.sprite = sprite;

            if (_spriteRenderer != null)
                _spriteRenderer.sprite = sprite;
        }
    }
}

using UnityEngine;

namespace Systems
{
    public enum SfxId
    {
        Slice,
        Drop,
        Chew,
        GlassBreak,
        Ding,
        Win,
        Lose,
        Bark,
        Spring,
        Click,
        ShortCircuit,
        WingFlap
    }

    public class SfxLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip _slice;
        [SerializeField] private AudioClip _drop;
        [SerializeField] private AudioClip _chew;
        [SerializeField] private AudioClip _glassBreak;
        [SerializeField] private AudioClip _ding;
        [SerializeField] private AudioClip _win;
        [SerializeField] private AudioClip _lose;
        [SerializeField] private AudioClip _bark;
        [SerializeField] private AudioClip _spring;
        [SerializeField] private AudioClip _click;
        [SerializeField] private AudioClip _shortCircuit;
        [SerializeField] private AudioClip _wingFlap;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        [Header("BGM")]
        [SerializeField] private AudioClip _bgm;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.5f;

        [Header("Start Offsets")]
        [SerializeField, Min(0f)] private float _sliceStartOffset;
        [SerializeField, Min(0f)] private float _dropStartOffset;
        [SerializeField, Min(0f)] private float _chewStartOffset;
        [SerializeField, Min(0f)] private float _glassBreakStartOffset;
        [SerializeField, Min(0f)] private float _dingStartOffset;
        [SerializeField, Min(0f)] private float _winStartOffset;
        [SerializeField, Min(0f)] private float _loseStartOffset;
        [SerializeField, Min(0f)] private float _barkStartOffset;
        [SerializeField, Min(0f)] private float _springStartOffset;
        [SerializeField, Min(0f)] private float _clickStartOffset;
        [SerializeField, Min(0f)] private float _shortCircuitStartOffset;
        [SerializeField, Min(0f)] private float _wingFlapStartOffset;

        public float Volume => _volume;
        public AudioClip Bgm => _bgm;
        public float BgmVolume => _bgmVolume;

        public AudioClip GetClip(SfxId id)
        {
            switch (id)
            {
                case SfxId.Slice:
                    return _slice;
                case SfxId.Drop:
                    return _drop;
                case SfxId.Chew:
                    return _chew;
                case SfxId.GlassBreak:
                    return _glassBreak;
                case SfxId.Ding:
                    return _ding;
                case SfxId.Win:
                    return _win;
                case SfxId.Lose:
                    return _lose;
                case SfxId.Bark:
                    return _bark;
                case SfxId.Spring:
                    return _spring;
                case SfxId.Click:
                    return _click;
                case SfxId.ShortCircuit:
                    return _shortCircuit;
                case SfxId.WingFlap:
                    return _wingFlap;
                default:
                    return null;
            }
        }

        public float GetStartOffset(SfxId id)
        {
            switch (id)
            {
                case SfxId.Slice:
                    return _sliceStartOffset;
                case SfxId.Drop:
                    return _dropStartOffset;
                case SfxId.Chew:
                    return _chewStartOffset;
                case SfxId.GlassBreak:
                    return _glassBreakStartOffset;
                case SfxId.Ding:
                    return _dingStartOffset;
                case SfxId.Win:
                    return _winStartOffset;
                case SfxId.Lose:
                    return _loseStartOffset;
                case SfxId.Bark:
                    return _barkStartOffset;
                case SfxId.Spring:
                    return _springStartOffset;
                case SfxId.Click:
                    return _clickStartOffset;
                case SfxId.ShortCircuit:
                    return _shortCircuitStartOffset;
                case SfxId.WingFlap:
                    return _wingFlapStartOffset;
                default:
                    return 0f;
            }
        }
    }
}

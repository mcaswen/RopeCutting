using System;
using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Systems.Dialogue
{
    public enum DialogueAdvanceMode
    {
        UseLineSetting,
        WaitForClickAfterEachLine,
        AutoAdvanceAfterEachLine
    }

    public enum DialogueClickDuringVoice
    {
        Ignore,
        SkipVoiceAndWait,
        AdvanceLine
    }

    public sealed class DialoguePlaybackLine
    {
        public DialoguePlaybackLine(DialogueSequence sequence, DialogueLine line, int lineIndex, DialogueLanguage language)
        {
            Sequence = sequence;
            Line = line;
            LineIndex = lineIndex;
            Language = language;
            Text = line != null ? line.GetText(language) : string.Empty;
        }

        public DialogueSequence Sequence { get; }
        public DialogueLine Line { get; }
        public int LineIndex { get; }
        public int LineNumber => LineIndex + 1;
        public DialogueLanguage Language { get; }
        public string Text { get; }
        public string SequenceId => Sequence != null ? Sequence.id : string.Empty;
        public DialogueSpeaker Speaker => Line != null ? Line.speaker : DialogueSpeaker.Narrator;
        public string Emotion => Line != null ? Line.emotion : string.Empty;
        public AudioClip VoiceClip => Line != null ? Line.voiceClip : null;
        public bool HasVoice => VoiceClip != null;
        public bool IsWait => Line != null && Line.lineType == DialogueLineType.Wait;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class DialoguePlayer : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DialogueDatabase _database;
        [SerializeField] private string _defaultSequenceId;
        [SerializeField] private DialogueLanguage _language = DialogueLanguage.English;
        [SerializeField] private bool _playOnStart;

        [Header("Advance")]
        [SerializeField] private DialogueAdvanceMode _advanceMode = DialogueAdvanceMode.WaitForClickAfterEachLine;
        [SerializeField] private DialogueClickDuringVoice _clickDuringVoice = DialogueClickDuringVoice.SkipVoiceAndWait;
        [SerializeField] private bool _acceptScreenClick = true;
        [SerializeField] private bool _ignorePointerOverUi = true;
        [SerializeField] private KeyCode _advanceKey = KeyCode.Space;
        [SerializeField, Min(0f)] private float _minimumVisibleSeconds = 0.08f;

        [Header("Playback")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private bool _useUnscaledTime;
        [SerializeField] private bool _lockGameplayInputDuringPlayback = true;
        [SerializeField] private bool _clearTextOnComplete = true;
        [SerializeField] private bool _stopAudioOnStop = true;

        [Header("Unity Events")]
        [SerializeField] private UnityEvent<string> _onSequenceStarted;
        [SerializeField] private UnityEvent<string> _onSequenceCompleted;
        [SerializeField] private UnityEvent<string> _onTextChanged;
        [SerializeField] private UnityEvent<string> _onSpeakerChanged;
        [SerializeField] private UnityEvent<string> _onEmotionChanged;
        [SerializeField] private UnityEvent<int> _onLineNumberChanged;
        [SerializeField] private UnityEvent _onWaitingForAdvance;
        [SerializeField] private UnityEvent _onPlaybackStopped;

        private Coroutine _playbackRoutine;
        private DialogueSequence _currentSequence;
        private DialoguePlaybackLine _currentLine;
        private bool _isPlaying;
        private bool _isWaitingForAdvance;
        private bool _lineAdvanceRequested;
        private bool _voiceSkipRequested;
        private bool _lockedGameplayInput;
        private int _ignoreAdvanceInputUntilFrame = -1;

        public event Action<DialogueSequence> SequenceStarted;
        public event Action<DialogueSequence> SequenceCompleted;
        public event Action<DialoguePlaybackLine> LineStarted;
        public event Action<DialoguePlaybackLine> LineFinished;
        public event Action WaitingForAdvance;
        public event Action PlaybackStopped;

        public bool IsPlaying => _isPlaying;
        public bool IsWaitingForAdvance => _isWaitingForAdvance;
        public bool IsVoicePlaying => _audioSource != null && _audioSource.isPlaying;
        public DialogueSequence CurrentSequence => _currentSequence;
        public DialoguePlaybackLine CurrentLine => _currentLine;
        public string CurrentText => _currentLine != null ? _currentLine.Text : string.Empty;
        public DialogueLanguage Language => _language;

        private void Awake()
        {
            ResolveAudioSource();
        }

        private void Start()
        {
            if (_playOnStart)
                PlayDefaultSequence();
        }

        private void Update()
        {
            if (!_isPlaying || Time.frameCount <= _ignoreAdvanceInputUntilFrame)
                return;

            if (WasAdvancePressed())
                Advance();
        }

        private void OnDisable()
        {
            StopPlayback();
        }

        private void OnDestroy()
        {
            UnlockGameplayInput();
        }

        public void PlayDefaultSequence()
        {
            PlaySequence(_defaultSequenceId);
        }

        public void PlaySequence(string sequenceId)
        {
            if (_database == null)
            {
                Debug.LogWarning($"{nameof(DialoguePlayer)} has no dialogue database.", this);
                return;
            }

            if (!_database.TryGetSequence(sequenceId, out DialogueSequence sequence))
            {
                Debug.LogWarning($"{nameof(DialoguePlayer)} could not find dialogue sequence '{sequenceId}'.", this);
                return;
            }

            PlaySequence(sequence);
        }

        public void PlaySequence(DialogueSequence sequence)
        {
            if (sequence == null)
                return;

            StopPlayback(false);

            _currentSequence = sequence;
            _ignoreAdvanceInputUntilFrame = Time.frameCount;
            _playbackRoutine = StartCoroutine(PlaybackRoutine(sequence));
        }

        public void StopPlayback()
        {
            StopPlayback(true);
        }

        public void Advance()
        {
            if (!_isPlaying)
                return;

            if (_audioSource != null && _audioSource.isPlaying)
            {
                if (_clickDuringVoice == DialogueClickDuringVoice.Ignore)
                    return;

                if (_clickDuringVoice == DialogueClickDuringVoice.SkipVoiceAndWait)
                {
                    _voiceSkipRequested = true;
                    _audioSource.Stop();
                    return;
                }

                _lineAdvanceRequested = true;
                _audioSource.Stop();
                return;
            }

            _lineAdvanceRequested = true;
        }

        public void SetLanguage(DialogueLanguage language)
        {
            _language = language;
        }

        public void SetLanguageChinese()
        {
            SetLanguage(DialogueLanguage.Chinese);
        }

        public void SetLanguageEnglish()
        {
            SetLanguage(DialogueLanguage.English);
        }

        private IEnumerator PlaybackRoutine(DialogueSequence sequence)
        {
            _isPlaying = true;
            LockGameplayInput();

            SequenceStarted?.Invoke(sequence);
            _onSequenceStarted?.Invoke(sequence.id);

            for (int i = 0; i < sequence.lines.Count; i++)
            {
                DialogueLine line = sequence.lines[i];
                if (line == null)
                    continue;

                yield return PlayLineRoutine(sequence, line, i);
            }

            CompletePlayback(sequence);
        }

        private IEnumerator PlayLineRoutine(DialogueSequence sequence, DialogueLine line, int lineIndex)
        {
            _lineAdvanceRequested = false;
            _voiceSkipRequested = false;
            _isWaitingForAdvance = false;

            yield return WaitSecondsOrAdvance(line.waitBefore, true);
            _lineAdvanceRequested = false;

            _currentLine = new DialoguePlaybackLine(sequence, line, lineIndex, _language);

            LineStarted?.Invoke(_currentLine);
            _onLineNumberChanged?.Invoke(_currentLine.LineNumber);
            _onSpeakerChanged?.Invoke(_currentLine.Speaker.ToString());
            _onEmotionChanged?.Invoke(_currentLine.Emotion);
            _onTextChanged?.Invoke(_currentLine.IsWait ? string.Empty : _currentLine.Text);

            if (_currentLine.IsWait)
            {
                yield return WaitSecondsOrAdvance(line.waitAfter, true);
                FinishCurrentLine();
                yield break;
            }

            PlayVoice(line.voiceClip);

            yield return WaitSecondsOrAdvance(_minimumVisibleSeconds, false);
            yield return WaitForVoice();

            if (_lineAdvanceRequested)
            {
                FinishCurrentLine();
                yield break;
            }

            if (ShouldAutoAdvance(line))
            {
                yield return WaitSecondsOrAdvance(line.waitAfter, true);
                FinishCurrentLine();
                yield break;
            }

            _isWaitingForAdvance = true;
            WaitingForAdvance?.Invoke();
            _onWaitingForAdvance?.Invoke();

            while (_isPlaying && !_lineAdvanceRequested)
                yield return null;

            _isWaitingForAdvance = false;
            FinishCurrentLine();
        }

        private void FinishCurrentLine()
        {
            if (_currentLine != null)
                LineFinished?.Invoke(_currentLine);

            _lineAdvanceRequested = false;
            _voiceSkipRequested = false;
        }

        private void CompletePlayback(DialogueSequence sequence)
        {
            _isPlaying = false;
            _isWaitingForAdvance = false;
            _playbackRoutine = null;

            if (_clearTextOnComplete)
                _onTextChanged?.Invoke(string.Empty);

            SequenceCompleted?.Invoke(sequence);
            _onSequenceCompleted?.Invoke(sequence.id);

            _currentLine = null;
            _currentSequence = null;
            UnlockGameplayInput();
        }

        private void StopPlayback(bool invokeEvent)
        {
            if (_playbackRoutine != null)
            {
                StopCoroutine(_playbackRoutine);
                _playbackRoutine = null;
            }

            if (_stopAudioOnStop && _audioSource != null)
                _audioSource.Stop();

            bool wasPlaying = _isPlaying;
            _isPlaying = false;
            _isWaitingForAdvance = false;
            _lineAdvanceRequested = false;
            _voiceSkipRequested = false;
            _currentLine = null;
            _currentSequence = null;
            UnlockGameplayInput();

            if (invokeEvent && wasPlaying)
            {
                _onTextChanged?.Invoke(string.Empty);
                PlaybackStopped?.Invoke();
                _onPlaybackStopped?.Invoke();
            }
        }

        private bool ShouldAutoAdvance(DialogueLine line)
        {
            switch (_advanceMode)
            {
                case DialogueAdvanceMode.UseLineSetting:
                    return line.autoAdvance;
                case DialogueAdvanceMode.AutoAdvanceAfterEachLine:
                    return true;
                default:
                    return false;
            }
        }

        private void PlayVoice(AudioClip voiceClip)
        {
            if (_audioSource == null || voiceClip == null)
                return;

            _audioSource.Stop();
            _audioSource.clip = voiceClip;
            _audioSource.Play();
        }

        private IEnumerator WaitForVoice()
        {
            while (_isPlaying && _audioSource != null && _audioSource.isPlaying)
            {
                if (_lineAdvanceRequested)
                    yield break;

                if (_voiceSkipRequested)
                {
                    _voiceSkipRequested = false;
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator WaitSecondsOrAdvance(float seconds, bool allowAdvanceToSkip)
        {
            if (seconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (_isPlaying && elapsed < seconds)
            {
                if (allowAdvanceToSkip && _lineAdvanceRequested)
                    yield break;

                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        private bool WasAdvancePressed()
        {
            if (_advanceKey != KeyCode.None && Input.GetKeyDown(_advanceKey))
                return true;

            if (!_acceptScreenClick)
                return false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                return touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId);
            }

            return Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1);
        }

        private bool IsPointerOverUi(int pointerId)
        {
            if (!_ignorePointerOverUi || EventSystem.current == null)
                return false;

            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private void ResolveAudioSource()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void LockGameplayInput()
        {
            if (!_lockGameplayInputDuringPlayback || _lockedGameplayInput)
                return;

            PlayerInputLock.Lock(this);
            _lockedGameplayInput = true;
        }

        private void UnlockGameplayInput()
        {
            if (!_lockedGameplayInput)
                return;

            PlayerInputLock.Unlock(this);
            _lockedGameplayInput = false;
        }
    }
}

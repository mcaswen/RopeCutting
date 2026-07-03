using System;
using System.Collections;
using Gameplay.Rope;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Systems.Dialogue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class RopeSubtitleSequencePlayer : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DialogueDatabase _database;
        [SerializeField] private string _defaultSequenceId;
        [SerializeField] private DialogueLanguage _language = DialogueLanguage.English;
        [SerializeField] private bool _playOnStart;

        [Header("Prefabs")]
        [SerializeField] private AdaptiveSubtitleBubble _subtitlePrefab;
        [SerializeField] private GameObject _anchorWithRopePrefab;
        [SerializeField] private Material _runtimeRopeMaterial;
        [SerializeField] private Transform _subtitleParent;
        [SerializeField] private Transform _ropeParent;

        [Header("Shared Positions")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _connectAnchorPoint;
        [SerializeField] private Vector3 _spawnPosition;
        [SerializeField] private Vector3 _connectAnchorPosition;

        [Header("Per-Subtitle Settle Points")]
        [FormerlySerializedAs("_settlePoint")]
        [SerializeField] private Transform _defaultSettlePoint;
        [FormerlySerializedAs("_settlePosition")]
        [SerializeField] private Vector3 _defaultSettlePosition;
        [SerializeField] private SubtitleSettleTarget[] _settleTargets = new SubtitleSettleTarget[0];

        [Header("Fall")]
        [SerializeField] private Vector2 _initialVelocity;
        [SerializeField] private float _gravityScale = 1f;
        [SerializeField] private bool _connectOnVerticalPass = true;
        [SerializeField, Min(0f)] private float _connectDistance = 0.3f;
        [SerializeField, Min(0.1f)] private float _maxFallSeconds = 8f;

        [Header("Settle")]
        [SerializeField, Min(0f)] private float _settleStartDistance = 0.5f;
        [SerializeField, Min(0.01f)] private float _settleDuration = 0.45f;
        [SerializeField, Min(0f)] private float _settleBackOvershoot = 1.7f;
        [SerializeField] private bool _freezeAtSettledPoint = true;

        [Header("Playback")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private bool _useUnscaledTime;
        [SerializeField, Min(0f)] private float _defaultLineInterval = 0.15f;
        [SerializeField] private bool _useLineWaitAfter = true;
        [SerializeField, Min(0f)] private float _noVoiceVisibleSeconds = 1.2f;

        [Header("Rope")]
        [SerializeField] private bool _indestructibleRopes = true;
        [SerializeField] private bool _disableRopeNodeColliders = true;
        [SerializeField] private int _runtimeRopeSortingOrder = 6;

        [Header("Lifetime")]
        [SerializeField] private bool _destroySubtitleAfterLine = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> _onSequenceStarted;
        [SerializeField] private UnityEvent<string> _onSequenceCompleted;
        [SerializeField] private UnityEvent<int> _onLineStarted;
        [SerializeField] private UnityEvent<string> _onSubtitleSettled;
        [SerializeField] private UnityEvent _onPlaybackStopped;

        private Coroutine _sequenceRoutine;
        private Coroutine _settleRoutine;
        private ActiveSubtitle _activeSubtitle;

        [Serializable]
#pragma warning disable 0649
        private struct SubtitleSettleTarget
        {
            public Transform Point;
            public Vector3 Position;
        }
#pragma warning restore 0649

        private sealed class ActiveSubtitle
        {
            public AdaptiveSubtitleBubble View;
            public RopeSubtitleBubble Connectable;
            public GameObject RopeRoot;

            public bool IsAlive => Connectable != null;

            public GameObject BubbleRoot
            {
                get
                {
                    if (Connectable != null)
                        return Connectable.gameObject;

                    return View != null ? View.gameObject : null;
                }
            }
        }

        private void Awake()
        {
            ResolveAudioSource();
        }

        private void Start()
        {
            if (_playOnStart)
                PlayDefaultSequence();
        }

        private void OnDisable()
        {
            StopSequence();
        }

        public void PlayDefaultSequence()
        {
            PlaySequence(_defaultSequenceId);
        }

        public void PlaySequence(string sequenceId)
        {
            if (_database == null)
            {
                Debug.LogWarning($"{nameof(RopeSubtitleSequencePlayer)} has no dialogue database.", this);
                return;
            }

            if (!_database.TryGetSequence(sequenceId, out DialogueSequence sequence))
            {
                Debug.LogWarning($"{nameof(RopeSubtitleSequencePlayer)} could not find dialogue sequence '{sequenceId}'.", this);
                return;
            }

            PlaySequence(sequence);
        }

        public void PlaySequence(DialogueSequence sequence)
        {
            if (sequence == null)
                return;

            StopSequence(false);
            _sequenceRoutine = StartCoroutine(SequenceRoutine(sequence));
        }

        public void StopSequence()
        {
            StopSequence(true);
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

        private IEnumerator SequenceRoutine(DialogueSequence sequence)
        {
            _onSequenceStarted?.Invoke(sequence.id);

            int subtitleIndex = 0;
            for (int i = 0; i < sequence.lines.Count; i++)
            {
                DialogueLine line = sequence.lines[i];
                if (line == null)
                    continue;

                yield return WaitSeconds(line.waitBefore);

                if (line.lineType == DialogueLineType.Wait)
                {
                    yield return WaitSeconds(GetLineInterval(line));
                    continue;
                }

                string text = line.GetText(_language);
                if (string.IsNullOrWhiteSpace(text) && line.voiceClip == null)
                {
                    yield return WaitSeconds(GetLineInterval(line));
                    continue;
                }

                DestroyActiveSubtitle();
                ActiveSubtitle subtitle = SpawnSubtitle(text);
                if (subtitle == null)
                    yield break;

                Vector3 settlePosition = ResolveSettlePosition(subtitleIndex);
                subtitleIndex++;

                _onLineStarted?.Invoke(i + 1);

                yield return WaitUntilConnectPoint(subtitle);
                if (!subtitle.IsAlive)
                    continue;

                subtitle.RopeRoot = CreateRopeInstance(subtitle.Connectable);

                yield return WaitUntilSettlePoint(subtitle, settlePosition);
                if (!subtitle.IsAlive)
                    continue;

                bool settleComplete = false;
                _settleRoutine = StartCoroutine(SettleSubtitle(subtitle, settlePosition, () => settleComplete = true));

                bool voiceStarted = PlayVoice(line.voiceClip);
                yield return WaitForVoiceAndSettle(voiceStarted, () => settleComplete);

                _onSubtitleSettled?.Invoke(text);
                yield return WaitSeconds(GetLineInterval(line));

                if (_destroySubtitleAfterLine)
                    DestroyActiveSubtitle();
            }

            _sequenceRoutine = null;
            _onSequenceCompleted?.Invoke(sequence.id);
        }

        private ActiveSubtitle SpawnSubtitle(string text)
        {
            if (_subtitlePrefab == null)
            {
                Debug.LogWarning($"{nameof(RopeSubtitleSequencePlayer)} has no subtitle prefab.", this);
                return null;
            }

            Vector3 spawnPosition = ResolvePoint(_spawnPoint, _spawnPosition);
            Quaternion spawnRotation = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
            AdaptiveSubtitleBubble view = Instantiate(_subtitlePrefab, spawnPosition, spawnRotation, _subtitleParent);
            view.gameObject.SetActive(true);
            view.SetText(text);

            RopeSubtitleBubble connectable = ResolveSubtitleConnectable(view);

            connectable.Bind(view);
            connectable.SetText(text);
            connectable.PrepareForDrop(_initialVelocity, _gravityScale);

            return new ActiveSubtitle
            {
                View = view,
                Connectable = connectable
            };
        }

        private RopeSubtitleBubble ResolveSubtitleConnectable(AdaptiveSubtitleBubble view)
        {
            Transform current = view.transform;
            while (current != null && current != _subtitleParent)
            {
                RopeSubtitleBubble connectable = current.GetComponent<RopeSubtitleBubble>();
                if (connectable != null)
                    return connectable;

                current = current.parent;
            }

            RopeSubtitleBubble childConnectable = view.GetComponentInChildren<RopeSubtitleBubble>(true);
            return childConnectable != null ? childConnectable : view.gameObject.AddComponent<RopeSubtitleBubble>();
        }

        private IEnumerator WaitUntilConnectPoint(ActiveSubtitle subtitle)
        {
            Vector3 spawnPosition = ResolvePoint(_spawnPoint, _spawnPosition);
            Vector3 connectPosition = ResolvePoint(_connectAnchorPoint, _connectAnchorPosition);
            bool startedAboveConnectPoint = spawnPosition.y >= connectPosition.y;

            float elapsed = 0f;
            while (subtitle.IsAlive && elapsed < _maxFallSeconds)
            {
                Vector3 currentPosition = subtitle.Connectable.transform.position;
                bool reachedByVerticalPass = _connectOnVerticalPass
                    && (startedAboveConnectPoint ? currentPosition.y <= connectPosition.y : currentPosition.y >= connectPosition.y);
                bool reachedByDistance = Vector2.Distance(currentPosition, connectPosition) <= _connectDistance;

                if (reachedByVerticalPass || reachedByDistance)
                    yield break;

                elapsed += DeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitUntilSettlePoint(ActiveSubtitle subtitle, Vector3 settlePosition)
        {
            float elapsed = 0f;
            while (subtitle.IsAlive && elapsed < _maxFallSeconds)
            {
                float distance = Vector2.Distance(subtitle.Connectable.transform.position, settlePosition);
                if (distance <= _settleStartDistance)
                    yield break;

                elapsed += DeltaTime;
                yield return null;
            }
        }

        private IEnumerator SettleSubtitle(ActiveSubtitle subtitle, Vector3 settlePosition, Action completed)
        {
            if (!subtitle.IsAlive)
            {
                completed?.Invoke();
                yield break;
            }

            RopeSubtitleBubble bubble = subtitle.Connectable;
            Vector3 startPosition = bubble.transform.position;

            Rigidbody2D body = bubble.Rigidbody;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.gravityScale = 0f;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            float elapsed = 0f;
            while (bubble != null && elapsed < _settleDuration)
            {
                elapsed += DeltaTime;
                float t = Mathf.Clamp01(elapsed / _settleDuration);
                float easedT = EaseOutBack(t, _settleBackOvershoot);
                bubble.MoveTo(Vector3.LerpUnclamped(startPosition, settlePosition, easedT));
                yield return null;
            }

            if (bubble != null)
                bubble.FixAt(settlePosition, _freezeAtSettledPoint);

            if (_settleRoutine != null)
                _settleRoutine = null;

            completed?.Invoke();
        }

        private IEnumerator WaitForVoiceAndSettle(bool voiceStarted, Func<bool> isSettleComplete)
        {
            float noVoiceTimer = voiceStarted ? 0f : _noVoiceVisibleSeconds;

            while (true)
            {
                bool waitingForSettle = isSettleComplete != null && !isSettleComplete();
                bool waitingForVoice = voiceStarted && _audioSource != null && _audioSource.isPlaying;
                bool waitingForNoVoiceTimer = noVoiceTimer > 0f;

                if (!waitingForSettle && !waitingForVoice && !waitingForNoVoiceTimer)
                    yield break;

                if (waitingForNoVoiceTimer)
                    noVoiceTimer -= DeltaTime;

                yield return null;
            }
        }

        private GameObject CreateRopeInstance(RopeConnectable connectable)
        {
            Vector3 anchorPosition = ResolvePoint(_connectAnchorPoint, _connectAnchorPosition);
            Quaternion anchorRotation = _connectAnchorPoint != null ? _connectAnchorPoint.rotation : Quaternion.identity;
            GameObject anchorRoot = _anchorWithRopePrefab != null
                ? Instantiate(_anchorWithRopePrefab, anchorPosition, anchorRotation, _ropeParent)
                : new GameObject("SubtitleRopeAnchor");

            if (_anchorWithRopePrefab == null && _ropeParent != null)
                anchorRoot.transform.SetParent(_ropeParent, true);

            anchorRoot.transform.position = anchorPosition;
            Rigidbody2D anchorBody = anchorRoot.GetComponent<Rigidbody2D>();
            if (anchorBody == null)
                anchorBody = anchorRoot.AddComponent<Rigidbody2D>();

            anchorBody.bodyType = RigidbodyType2D.Kinematic;
            anchorBody.gravityScale = 0f;
            anchorBody.velocity = Vector2.zero;
            anchorBody.angularVelocity = 0f;

            RopeController rope = anchorRoot.GetComponentInChildren<RopeController>(true);
            if (rope == null)
                rope = CreateRuntimeRope(anchorRoot.transform);

            rope.ConfigureAnchorToConnectable(anchorRoot.transform, connectable);
            rope.SetProtection(_indestructibleRopes, _disableRopeNodeColliders);
            return anchorRoot;
        }

        private RopeController CreateRuntimeRope(Transform anchorTransform)
        {
            GameObject ropeObject = new GameObject("Rope");
            ropeObject.transform.SetParent(anchorTransform, false);

            LineRenderer lineRenderer = ropeObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.sortingOrder = _runtimeRopeSortingOrder;
            if (_runtimeRopeMaterial != null)
                lineRenderer.sharedMaterial = _runtimeRopeMaterial;

            Vector3 anchorPosition = anchorTransform.position;
            lineRenderer.SetPosition(0, anchorPosition);
            lineRenderer.SetPosition(1, anchorPosition);

            return ropeObject.AddComponent<RopeController>();
        }

        private bool PlayVoice(AudioClip voiceClip)
        {
            ResolveAudioSource();

            if (_audioSource == null || voiceClip == null)
                return false;

            _audioSource.Stop();
            _audioSource.clip = voiceClip;
            _audioSource.Play();
            return true;
        }

        private float GetLineInterval(DialogueLine line)
        {
            if (_useLineWaitAfter && line != null && line.waitAfter > 0f)
                return line.waitAfter;

            return _defaultLineInterval;
        }

        private IEnumerator WaitSeconds(float seconds)
        {
            if (seconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += DeltaTime;
                yield return null;
            }
        }

        private void StopSequence(bool invokeEvent)
        {
            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            if (_settleRoutine != null)
            {
                StopCoroutine(_settleRoutine);
                _settleRoutine = null;
            }

            if (_audioSource != null)
                _audioSource.Stop();

            DestroyActiveSubtitle();

            if (invokeEvent)
                _onPlaybackStopped?.Invoke();
        }

        private void DestroyActiveSubtitle()
        {
            if (_activeSubtitle == null)
                return;

            if (_activeSubtitle.RopeRoot != null)
                Destroy(_activeSubtitle.RopeRoot);

            GameObject bubbleRoot = _activeSubtitle.BubbleRoot;
            if (bubbleRoot != null)
                Destroy(bubbleRoot);

            _activeSubtitle = null;
        }

        private void ResolveAudioSource()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private Vector3 ResolvePoint(Transform point, Vector3 fallback)
        {
            return point != null ? point.position : fallback;
        }

        private Vector3 ResolveSettlePosition(int subtitleIndex)
        {
            if (_settleTargets != null && subtitleIndex >= 0 && subtitleIndex < _settleTargets.Length)
                return ResolvePoint(_settleTargets[subtitleIndex].Point, _settleTargets[subtitleIndex].Position);

            return ResolvePoint(_defaultSettlePoint, _defaultSettlePosition);
        }

        private float DeltaTime => _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private static float EaseOutBack(float t, float overshoot)
        {
            float inverse = t - 1f;
            float c3 = overshoot + 1f;
            return 1f + c3 * inverse * inverse * inverse + overshoot * inverse * inverse;
        }
    }
}

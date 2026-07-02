using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    public static class SfxPlayer
    {
        private const string LibraryResourcePath = "SfxLibrary";
        private const int InitialSourceCount = 4;

        private static SfxLibrary _library;
        private static GameObject _host;
        private static readonly List<AudioSource> Sources = new List<AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void WarmUp()
        {
            SfxLibrary library = GetLibrary();
            GetHost();

            if (Sources.Count == 0)
            {
                for (int i = 0; i < InitialSourceCount; i++)
                    CreateSource();
            }

            if (library == null) return;

            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                AudioClip clip = library.GetClip(id);
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
            }
        }

        public static void Play(SfxId id)
        {
            SfxLibrary library = GetLibrary();
            if (library == null) return;

            AudioClip clip = library.GetClip(id);
            if (clip == null) return;

            AudioSource source = GetSource();
            if (source == null) return;

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            source.Stop();
            source.clip = clip;
            source.volume = library.Volume;
            source.loop = false;
            source.spatialBlend = 0f;
            ApplyStartOffset(source, clip, library.GetStartOffset(id));
            source.Play();
        }

        private static SfxLibrary GetLibrary()
        {
            if (_library == null)
                _library = Resources.Load<SfxLibrary>(LibraryResourcePath);

            return _library;
        }

        private static GameObject GetHost()
        {
            if (_host != null)
                return _host;

            _host = new GameObject("SfxPlayer");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            return _host;
        }

        private static AudioSource GetSource()
        {
            GetHost();

            for (int i = 0; i < Sources.Count; i++)
            {
                AudioSource source = Sources[i];
                if (source != null && !source.isPlaying)
                    return source;
            }

            return CreateSource();
        }

        private static AudioSource CreateSource()
        {
            AudioSource source = GetHost().AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            Sources.Add(source);
            return source;
        }

        private static void ApplyStartOffset(AudioSource source, AudioClip clip, float startOffset)
        {
            if (startOffset <= 0f || clip.length <= 0f)
            {
                source.timeSamples = 0;
                return;
            }

            float safeOffset = Mathf.Clamp(startOffset, 0f, Mathf.Max(0f, clip.length - 0.001f));

            try
            {
                source.timeSamples = Mathf.Clamp(
                    Mathf.RoundToInt(safeOffset * clip.frequency),
                    0,
                    Mathf.Max(0, clip.samples - 1));
            }
            catch
            {
                source.time = safeOffset;
            }
        }
    }
}

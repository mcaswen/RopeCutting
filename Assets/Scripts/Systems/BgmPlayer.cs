using UnityEngine;

namespace Systems
{
    public static class BgmPlayer
    {
        private const string LibraryResourcePath = "SfxLibrary";

        private static GameObject _host;
        private static AudioSource _source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PlayOnStartup()
        {
            Play();
        }

        public static void Play()
        {
            SfxLibrary library = Resources.Load<SfxLibrary>(LibraryResourcePath);
            if (library == null || library.Bgm == null)
                return;

            AudioSource source = GetSource();
            if (source == null)
                return;

            if (source.clip == library.Bgm && source.isPlaying)
                return;

            source.clip = library.Bgm;
            source.volume = library.BgmVolume;
            source.loop = true;
            source.spatialBlend = 0f;

            if (source.clip.loadState == AudioDataLoadState.Unloaded)
                source.clip.LoadAudioData();

            source.Play();
        }

        public static void Stop()
        {
            if (_source != null)
                _source.Stop();
        }

        private static AudioSource GetSource()
        {
            if (_source != null)
                return _source;

            _host = new GameObject("BgmPlayer");
            Object.DontDestroyOnLoad(_host);

            _source = _host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            return _source;
        }
    }
}

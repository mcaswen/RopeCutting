using Systems;
using UnityEditor;
using UnityEngine;

public static class SfxStartOffsetAnalyzer
{
    private const string LibraryPath = "Assets/Resources/SfxLibrary.asset";
    private const float AbsoluteThreshold = 0.003f;
    private const float RelativePeakThreshold = 0.04f;
    private const float PreRollSeconds = 0.004f;
    private const int ConsecutiveFrames = 32;

    private static readonly Entry[] Entries =
    {
        new Entry("Slice", "_slice", "_sliceStartOffset"),
        new Entry("Drop", "_drop", "_dropStartOffset"),
        new Entry("Chew", "_chew", "_chewStartOffset"),
        new Entry("GlassBreak", "_glassBreak", "_glassBreakStartOffset"),
        new Entry("Ding", "_ding", "_dingStartOffset"),
        new Entry("Win", "_win", "_winStartOffset"),
        new Entry("Lose", "_lose", "_loseStartOffset"),
        new Entry("Bark", "_bark", "_barkStartOffset"),
        new Entry("Spring", "_spring", "_springStartOffset"),
        new Entry("Click", "_click", "_clickStartOffset"),
        new Entry("ShortCircuit", "_shortCircuit", "_shortCircuitStartOffset"),
        new Entry("WingFlap", "_wingFlap", "_wingFlapStartOffset"),
    };

    [MenuItem("Tools/SFX/Analyze Start Offsets")]
    public static void AnalyzeDefaultLibrary()
    {
        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        AnalyzeLibrary(library);
    }

    public static void AnalyzeLibrary(SfxLibrary library)
    {
        if (library == null)
        {
            Debug.LogError($"SfxLibrary not found at {LibraryPath}.");
            return;
        }

        SerializedObject serializedLibrary = new SerializedObject(library);

        try
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];
                SerializedProperty clipProperty = serializedLibrary.FindProperty(entry.ClipProperty);
                SerializedProperty offsetProperty = serializedLibrary.FindProperty(entry.OffsetProperty);

                if (clipProperty == null || offsetProperty == null)
                    continue;

                AudioClip clip = clipProperty.objectReferenceValue as AudioClip;
                if (clip == null)
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Analyze SFX Start Offsets",
                    clip.name,
                    (float)i / Entries.Length);

                float offset = AnalyzeClip(clip);
                offsetProperty.floatValue = offset;
                Debug.Log($"[SFX] {entry.Name}: start offset = {offset:0.0000}s", clip);
            }

            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static float AnalyzeClip(AudioClip clip)
    {
        string assetPath = AssetDatabase.GetAssetPath(clip);
        AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;

        if (importer == null)
            return AnalyzeLoadedClip(clip);

        AudioImporterSampleSettings originalSettings = importer.defaultSampleSettings;
        bool originalLoadInBackground = importer.loadInBackground;

        bool needsReadableReimport =
            originalSettings.loadType != AudioClipLoadType.DecompressOnLoad ||
            !originalSettings.preloadAudioData ||
            originalLoadInBackground;

        if (!needsReadableReimport)
            return AnalyzeLoadedClip(clip);

        try
        {
            AudioImporterSampleSettings readableSettings = originalSettings;
            readableSettings.loadType = AudioClipLoadType.DecompressOnLoad;
            readableSettings.preloadAudioData = true;
            importer.defaultSampleSettings = readableSettings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();

            AudioClip readableClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            return AnalyzeLoadedClip(readableClip != null ? readableClip : clip);
        }
        finally
        {
            importer.defaultSampleSettings = originalSettings;
            importer.loadInBackground = originalLoadInBackground;
            importer.SaveAndReimport();
        }
    }

    private static float AnalyzeLoadedClip(AudioClip clip)
    {
        if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
            return 0f;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        int channels = Mathf.Max(1, clip.channels);
        float[] samples = new float[clip.samples * channels];

        if (!clip.GetData(samples, 0))
            return 0f;

        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));

        float threshold = Mathf.Max(AbsoluteThreshold, peak * RelativePeakThreshold);
        int consecutive = 0;
        int firstFrame = 0;

        for (int frame = 0; frame < clip.samples; frame++)
        {
            float framePeak = 0f;
            int sampleStart = frame * channels;

            for (int channel = 0; channel < channels; channel++)
                framePeak = Mathf.Max(framePeak, Mathf.Abs(samples[sampleStart + channel]));

            if (framePeak >= threshold)
            {
                if (consecutive == 0)
                    firstFrame = frame;

                consecutive++;
                if (consecutive >= ConsecutiveFrames)
                    return Mathf.Max(0f, firstFrame / (float)clip.frequency - PreRollSeconds);
            }
            else
            {
                consecutive = 0;
            }
        }

        return 0f;
    }

    private readonly struct Entry
    {
        public Entry(string name, string clipProperty, string offsetProperty)
        {
            Name = name;
            ClipProperty = clipProperty;
            OffsetProperty = offsetProperty;
        }

        public string Name { get; }
        public string ClipProperty { get; }
        public string OffsetProperty { get; }
    }
}

[CustomEditor(typeof(SfxLibrary))]
public class SfxLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Analyze Start Offsets"))
            SfxStartOffsetAnalyzer.AnalyzeLibrary((SfxLibrary)target);
    }
}

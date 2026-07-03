using Systems.Dialogue;
using UnityEditor;
using UnityEngine;

// 不使用命名空间，避免与 UnityEditor 冲突
public static class DialogueSetupTool
{
    private const string PrefabPath = "Assets/Prefabs/Pfb_DialogueSystem.prefab";
    private const string DatabasePath = "Assets/SO/DialogueDatabase.asset";

    [MenuItem("Tools/Dialogue/Setup Dialogue System Prefab")]
    public static void CreateDialogueSystemPrefab()
    {
        var database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
        if (database == null)
        {
            Debug.LogError($"未找到 DialogueDatabase，路径: {DatabasePath}");
            return;
        }

        // 创建根对象（DialogueManager）
        var root = new GameObject("Pfb_DialogueSystem");
        var manager = root.AddComponent<DialogueManager>();

        // 创建子对象（DialoguePlayer）
        var playerGo = new GameObject("DialoguePlayer");
        playerGo.transform.SetParent(root.transform);
        var player = playerGo.AddComponent<DialoguePlayer>();
        var audioSource = playerGo.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D

        // 反射设置 _database
        SerializedObject so = new SerializedObject(player);
        so.FindProperty("_database").objectReferenceValue = database;
        so.FindProperty("_advanceMode").enumValueIndex = (int)DialogueAdvanceMode.UseLineSetting;
        so.FindProperty("_clickDuringVoice").enumValueIndex = (int)DialogueClickDuringVoice.SkipVoiceAndWait;
        so.FindProperty("_acceptScreenClick").boolValue = true;
        so.FindProperty("_lockGameplayInputDuringPlayback").boolValue = true;
        so.ApplyModifiedProperties();

        // 反射设置 manager._player
        SerializedObject mgrSo = new SerializedObject(manager);
        mgrSo.FindProperty("_player").objectReferenceValue = player;
        mgrSo.ApplyModifiedProperties();

        // 创建预制体
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"Dialogue 系统预制体已创建: {PrefabPath}");
    }
}

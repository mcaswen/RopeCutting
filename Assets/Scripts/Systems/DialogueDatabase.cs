using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.Dialogue
{
    public enum DialogueLanguage
    {
        Chinese,
        English
    }

    public enum DialogueSpeaker
    {
        Narrator,
        Bird,
        System
    }

    public enum DialogueLineType
    {
        Dialogue,
        Wait
    }

    [Serializable]
    public sealed class DialogueLine
    {
        public DialogueLineType lineType = DialogueLineType.Dialogue;
        public DialogueSpeaker speaker = DialogueSpeaker.Narrator;
        [TextArea(2, 5)] public string zh;
        [TextArea(2, 5)] public string en;
        [Min(0f)] public float waitBefore;
        [Min(0f)] public float waitAfter = 0.15f;
        public bool autoAdvance = true;
        public AudioClip voiceClip;
        public string emotion;

        public string GetText(DialogueLanguage language)
        {
            string primary = language == DialogueLanguage.Chinese ? zh : en;
            if (!string.IsNullOrWhiteSpace(primary))
                return primary;

            return language == DialogueLanguage.Chinese ? en : zh;
        }
    }

    [Serializable]
    public sealed class DialogueSequence
    {
        public string id;
        public string description;
        public List<DialogueLine> lines = new List<DialogueLine>();
    }

    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "Rope Cutting/Dialogue Database")]
    public sealed class DialogueDatabase : ScriptableObject
    {
        public List<DialogueSequence> sequences = new List<DialogueSequence>();

        public bool TryGetSequence(string id, out DialogueSequence sequence)
        {
            sequence = GetSequenceOrNull(id);
            return sequence != null;
        }

        public DialogueSequence GetSequenceOrNull(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < sequences.Count; i++)
            {
                DialogueSequence sequence = sequences[i];
                if (sequence != null && sequence.id == id)
                    return sequence;
            }

            return null;
        }
    }
}

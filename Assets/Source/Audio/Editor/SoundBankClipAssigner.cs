using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Audio.Editor
{
    // Appends an imported clip into the private _clips array of the SfxEntry at the GameSoundId
    // ordinal, through SerializedObject (the EnumIndexed array is a plain nested serialized array).
    internal static class SoundBankClipAssigner
    {
        public static void Assign(SoundBankConfiguration bank, GameSoundId soundId, AudioClip clip)
        {
            if (bank == null || clip == null)
            {
                return;
            }

            var serialized = new SerializedObject(bank);
            var entries = serialized.FindProperty("_entries");
            var index = (int)soundId;
            if (entries == null || index < 0 || index >= entries.arraySize)
            {
                Debug.LogWarning($"[SFX Fetch] SoundBankConfiguration has no entry slot for {soundId}.");
                return;
            }

            var clips = entries.GetArrayElementAtIndex(index).FindPropertyRelative("_clips");
            var insertAt = clips.arraySize;
            clips.InsertArrayElementAtIndex(insertAt);
            clips.GetArrayElementAtIndex(insertAt).objectReferenceValue = clip;
            serialized.ApplyModifiedProperties();
        }
    }
}

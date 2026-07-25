using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Draws a <see cref="MusicalNoteAttribute" /> int field as a note-name dropdown.</summary>
    [CustomPropertyDrawer(typeof(MusicalNoteAttribute))]
    internal sealed class MusicalNoteDrawer : PropertyDrawer
    {
        private static readonly string[] NoteNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B",
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var note = (MusicalNoteAttribute)attribute;
            var min = note.MinSemitone;
            var max = Mathf.Max(min, note.MaxSemitone);

            // Non-int fields, or values outside the note range, degrade to the default field so a
            // hand-typed/out-of-range semitone is never silently clamped.
            if (property.propertyType != SerializedPropertyType.Integer
                || property.intValue < min || property.intValue > max)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var count = max - min + 1;
            var values = new int[count];
            var labels = new GUIContent[count];
            for (var i = 0; i < count; i++)
            {
                var semitone = min + i;
                values[i] = semitone;
                labels[i] = new GUIContent(LabelFor(semitone));
            }

            property.intValue = EditorGUI.IntPopup(position, label, property.intValue, labels, values);
        }

        private static string LabelFor(int semitone)
        {
            var pitchClass = ((semitone % 12) + 12) % 12;
            var octave = Mathf.FloorToInt(semitone / 12f);
            if (octave == 0)
            {
                return NoteNames[pitchClass];
            }

            return $"{NoteNames[pitchClass]} {(octave > 0 ? "+" : string.Empty)}{octave}";
        }
    }
}

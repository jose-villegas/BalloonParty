using BalloonParty.Game;
using BalloonParty.Item;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace BalloonParty.Configuration.Editor
{
    /// <summary>
    ///     Adds a live, play-mode-only panel below the default inspector showing what
    ///     <see cref="ItemSightTracker" /> currently reports sighted — lets the item-sighted audio cue's
    ///     settle/stagger tuning above be judged against the actual chain it's reacting to, instead of
    ///     inferring the chain from the sound alone.
    /// </summary>
    [CustomEditor(typeof(ItemPreviewConfig))]
    internal sealed class ItemPreviewConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live Sighted Items", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see the live sighted-item chain.", MessageType.Info);
                return;
            }

            var scope = UnityEngine.Object.FindAnyObjectByType<GameLifetimeScope>();
            var tracker = scope?.Container.Resolve<ItemSightTracker>();
            if (tracker == null)
            {
                EditorGUILayout.HelpBox("No GameLifetimeScope found in the loaded scene.", MessageType.Warning);
                return;
            }

            var sighted = tracker.Sighted;
            if (sighted.Count == 0)
            {
                EditorGUILayout.HelpBox("(none)", MessageType.None);
            }
            else
            {
                for (var i = 0; i < sighted.Count; i++)
                {
                    var entry = sighted[i];
                    EditorGUILayout.LabelField($"{i + 1}.", $"{entry.Item} @ {entry.Slot}");
                }
            }

            Repaint();
        }
    }
}

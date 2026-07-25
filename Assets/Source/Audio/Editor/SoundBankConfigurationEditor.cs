using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Audio.Editor
{
    // Fetch UI lives on the SoundBankConfiguration inspector itself: each sound entry gets an inline
    // "Fetch clips (Freesound)" foldout hanging off it (shown only when the entry has a fetch prompt and
    // no clips yet). Fills the slot from the prompt via Freesound (CC0 + CC-BY), auditions in-editor,
    // and records attribution. Editor-only; reuses the provider/importer/assigner/ledger unchanged.
    [CustomEditor(typeof(SoundBankConfiguration))]
    internal sealed class SoundBankConfigurationEditor : UnityEditor.Editor
    {
        private const string AttributionAssetPath = "Assets/Resources/AudioAttributions.json";
        private const int MaxResults = 12;
        private const float MinDuration = 0.05f;
        private const float MaxDuration = 3f;

        private readonly FreesoundTokenSource _tokenSource = new();
        private readonly Dictionary<GameSoundId, List<SfxCandidate>> _candidates = new();
        private readonly HashSet<GameSoundId> _busy = new();
        private readonly HashSet<GameSoundId> _expanded = new();

        private ISfxProvider _provider;
        private string _tokenInput = string.Empty;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_entries");

            EditorGUILayout.Space();
            DrawTokenSection();

            var entries = serializedObject.FindProperty("_entries");
            if (entries != null)
            {
                EditorGUILayout.LabelField("Sounds", EditorStyles.boldLabel);
                for (var i = 0; i < entries.arraySize; i++)
                {
                    DrawEntry((SoundBankConfiguration)target, (GameSoundId)i, entries.GetArrayElementAtIndex(i));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTokenSection()
        {
            if (_tokenSource.TryResolve(out _, out var source))
            {
                EditorGUILayout.HelpBox($"Freesound token loaded from {source}.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "No Freesound token — fetch is disabled. Set the FREESOUND_API_TOKEN environment variable, " +
                "or paste one below (stored per-machine in EditorPrefs, never committed).", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _tokenInput = EditorGUILayout.PasswordField("API token", _tokenInput);
                if (GUILayout.Button("Save", GUILayout.Width(60)))
                {
                    EditorPrefs.SetString(FreesoundTokenSource.EditorPrefKey, _tokenInput?.Trim() ?? string.Empty);
                    _tokenInput = string.Empty;
                }
            }

            if (GUILayout.Button("Get a free Freesound API token"))
            {
                Application.OpenURL("https://freesound.org/apiv2/apply/");
            }
        }

        private void DrawEntry(SoundBankConfiguration bank, GameSoundId soundId, SerializedProperty entry)
        {
            EditorGUILayout.PropertyField(entry, new GUIContent(soundId.ToString()), true);

            var promptProp = entry.FindPropertyRelative("_fetchPrompt");
            var clipsProp = entry.FindPropertyRelative("_clips");
            var prompt = promptProp != null ? promptProp.stringValue : string.Empty;
            var hasClips = clipsProp != null && clipsProp.arraySize > 0;

            // The fetch foldout only appears where it's useful: a prompt to search with, no clip yet,
            // and a token to search with.
            if (string.IsNullOrWhiteSpace(prompt) || hasClips || !_tokenSource.HasToken)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var wasExpanded = _expanded.Contains(soundId);
                var expanded = EditorGUILayout.Foldout(wasExpanded, "Fetch clips (Freesound)", true);
                if (expanded && !wasExpanded)
                {
                    _expanded.Add(soundId);
                }
                else if (!expanded && wasExpanded)
                {
                    _expanded.Remove(soundId);
                }

                if (!expanded)
                {
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_busy.Contains(soundId)))
                    {
                        if (GUILayout.Button("Fetch candidates"))
                        {
                            FetchAsync(soundId, prompt);
                        }
                    }

                    if (GUILayout.Button("■ Stop", GUILayout.Width(60)))
                    {
                        StopPreview();
                    }
                }

                if (_candidates.TryGetValue(soundId, out var list))
                {
                    foreach (var candidate in list)
                    {
                        DrawCandidateRow(bank, soundId, candidate);
                    }
                }
            }
        }

        private void DrawCandidateRow(SoundBankConfiguration bank, GameSoundId soundId, SfxCandidate candidate)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var label = $"{candidate.Name} · {candidate.Author} · {candidate.LicenseName} · {candidate.Duration:0.0}s";
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);

                if (GUILayout.Button("▶", GUILayout.Width(28)))
                {
                    PreviewAsync(candidate.PreviewUrl);
                }

                if (GUILayout.Button("Site", GUILayout.Width(44)))
                {
                    Application.OpenURL(candidate.SoundUrl);
                }

                using (new EditorGUI.DisabledScope(_busy.Contains(soundId)))
                {
                    if (GUILayout.Button("Accept", GUILayout.Width(70)))
                    {
                        AcceptAsync(bank, soundId, candidate);
                    }
                }
            }
        }

        private static void StopPreview()
        {
            try
            {
                SfxPreviewAuditioner.Stop();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SFX Fetch] stop preview failed: {e.Message}");
            }
        }

        private async void FetchAsync(GameSoundId soundId, string prompt)
        {
            _provider ??= new FreesoundSfxProvider(_tokenSource);
            _busy.Add(soundId);
            try
            {
                var request = new SfxFetchRequest(prompt, SfxLicense.Cc0 | SfxLicense.AttributionBy, MaxResults,
                    MinDuration, MaxDuration);
                var results = await _provider.FetchAsync(request, CancellationToken.None);
                _candidates[soundId] = new List<SfxCandidate>(results);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SFX Fetch] fetch failed for {soundId}: {e.Message}");
            }
            finally
            {
                _busy.Remove(soundId);
                Repaint();
            }
        }

        private async void PreviewAsync(string previewUrl)
        {
            try
            {
                await SfxPreviewAuditioner.PlayAsync(previewUrl, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SFX Fetch] preview failed: {e.Message}");
            }
        }

        private async void AcceptAsync(SoundBankConfiguration bank, GameSoundId soundId, SfxCandidate candidate)
        {
            _busy.Add(soundId);
            try
            {
                var clip = await SfxClipImporter.ImportAsync(candidate, soundId, CancellationToken.None);
                if (clip == null)
                {
                    return;
                }

                SoundBankClipAssigner.Assign(bank, soundId, clip);
                RecordAttribution(soundId, candidate);
                _candidates.Remove(soundId);
                _expanded.Remove(soundId);
                serializedObject.Update();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SFX Fetch] accept failed for {soundId}: {e.Message}");
            }
            finally
            {
                _busy.Remove(soundId);
                Repaint();
            }
        }

        private static void RecordAttribution(GameSoundId soundId, SfxCandidate candidate)
        {
            var ledger = File.Exists(AttributionAssetPath)
                ? AttributionLedger.FromJson(File.ReadAllText(AttributionAssetPath))
                : new AttributionLedger();

            ledger.Merge(new AttributionRecord
            {
                SoundId = soundId.ToString(),
                ProviderId = candidate.ProviderId,
                Name = candidate.Name,
                Author = candidate.Author,
                SoundUrl = candidate.SoundUrl,
                LicenseName = candidate.LicenseName,
                LicenseUrl = candidate.LicenseUrl,
                RequiresAttribution = candidate.RequiresAttribution,
            });

            var directory = Path.GetDirectoryName(AttributionAssetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(AttributionAssetPath, ledger.ToJson());
            AssetDatabase.ImportAsset(AttributionAssetPath);
        }
    }
}

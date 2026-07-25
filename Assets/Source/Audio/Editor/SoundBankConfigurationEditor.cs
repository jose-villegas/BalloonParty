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
        private readonly HashSet<GameSoundId> _fetchExpanded = new();

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
                // Show the element count the way a default list header would — the array is
                // EnumIndexed (fixed to the GameSoundId count), so it isn't user-resizable.
                EditorGUILayout.LabelField($"Sounds ({entries.arraySize})", EditorStyles.boldLabel);
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
            if (!entry.isExpanded)
            {
                return;
            }

            // Fetching appends to the clip list, so it stays available even once a clip is assigned —
            // you can pull in more variations. Only a token is required.
            if (!_tokenSource.HasToken)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var expanded = EditorGUILayout.Foldout(_fetchExpanded.Contains(soundId), "Fetch (Freesound)", true);
                if (expanded)
                {
                    _fetchExpanded.Add(soundId);
                }
                else
                {
                    _fetchExpanded.Remove(soundId);
                    return;
                }

                var promptProp = entry.FindPropertyRelative("_fetchPrompt");
                if (promptProp != null)
                {
                    EditorGUILayout.PropertyField(promptProp, new GUIContent("Fetch Prompt"));
                }

                var prompt = promptProp != null ? promptProp.stringValue : string.Empty;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_busy.Contains(soundId) || string.IsNullOrWhiteSpace(prompt)))
                    {
                        if (GUILayout.Button("Fetch Candidates (Freesound)"))
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
                // Keep the candidate list and the open dropdown so more clips can be added in one go —
                // closing is left to the user.
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

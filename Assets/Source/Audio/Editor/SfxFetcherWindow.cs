using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Audio.Editor
{
    // Author-time tool: fills empty SfxEntry clip slots from each entry's fetch prompt via Freesound,
    // with a human accept-per-clip gate (the license/quality review) and attribution recording.
    internal sealed class SfxFetcherWindow : EditorWindow
    {
        private const string AttributionAssetPath = "Assets/Resources/AudioAttributions.json";
        private const int MaxResults = 12;
        private const float MinDuration = 0.05f;
        private const float MaxDuration = 3f;

        private readonly EditorAssetCache<SoundBankConfiguration> _bankCache = new();
        private readonly FreesoundTokenSource _tokenSource = new();
        private readonly Dictionary<GameSoundId, List<SfxCandidate>> _candidates = new();
        private readonly HashSet<GameSoundId> _busy = new();

        private ISfxProvider _provider;
        private string _tokenInput = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Tools/BalloonParty/SFX Fetcher")]
        private static void Open()
        {
            GetWindow<SfxFetcherWindow>("SFX Fetcher");
        }

        private void OnGUI()
        {
            DrawTokenSection();

            var bank = _bankCache.Value;
            if (bank == null)
            {
                EditorGUILayout.HelpBox("No SoundBankConfiguration asset found in the project.", MessageType.Warning);
                return;
            }

            if (!_tokenSource.HasToken)
            {
                EditorGUILayout.HelpBox("Set a Freesound token above to enable fetching.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sounds with a fetch prompt and no clips", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var serialized = new SerializedObject(bank);
            var entries = serialized.FindProperty("_entries");
            if (entries != null)
            {
                for (var i = 0; i < entries.arraySize; i++)
                {
                    DrawEntryRow(bank, (GameSoundId)i, entries.GetArrayElementAtIndex(i));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTokenSection()
        {
            if (_tokenSource.TryResolve(out _, out var source))
            {
                EditorGUILayout.HelpBox($"Freesound token loaded from {source}.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "No Freesound token. Set the FREESOUND_API_TOKEN environment variable, or paste one below " +
                "(stored per-machine in EditorPrefs, never committed).", MessageType.Warning);

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

        private void DrawEntryRow(SoundBankConfiguration bank, GameSoundId soundId, SerializedProperty entry)
        {
            var promptProp = entry.FindPropertyRelative("_fetchPrompt");
            var clipsProp = entry.FindPropertyRelative("_clips");
            var prompt = promptProp != null ? promptProp.stringValue : string.Empty;
            var hasClips = clipsProp != null && clipsProp.arraySize > 0;

            if (string.IsNullOrWhiteSpace(prompt) || hasClips)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(soundId.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(prompt, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(_busy.Contains(soundId)))
                {
                    if (GUILayout.Button("Fetch candidates"))
                    {
                        FetchAsync(soundId, prompt);
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

                if (GUILayout.Button("Listen", GUILayout.Width(60)))
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
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssets();
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

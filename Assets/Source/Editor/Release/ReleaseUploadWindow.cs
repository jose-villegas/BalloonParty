using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BalloonParty.Editor.Release
{
    /// <summary>
    ///     Editor window that builds three APK variants (release, release+cheats, development),
    ///     tags the commit, generates a changelog, and uploads everything to GitHub as a release.
    ///     <para/>
    ///     Build provenance is enforced by three layers:
    ///     <list type="number">
    ///         <item>The working tree must be clean (no uncommitted changes).</item>
    ///         <item>Commit SHA + git tree hash are baked into each APK via <c>BuildInfo.json</c>.</item>
    ///         <item>SHA-256 checksums of every APK are published in the release notes.</item>
    ///     </list>
    /// </summary>
    internal sealed class ReleaseUploadWindow : EditorWindow
    {
        private const string TokenPrefKey = "BalloonParty_GitHubToken";
        private const string BuildFolder = "Builds";
        private const string Repository = "jose-villegas/BalloonParty";
        private const string CheatsDefine = "CHEATS_IN_RELEASE";
        private const string BuildInfoPath = "Assets/Resources/BuildInfo.json";

        [SerializeField] private string version = "";
        [SerializeField] private string summary = "";

        private string outputLog = "";
        private Vector2 scrollPos;
        private bool isRunning;
        private bool showToken;
        private GUIStyle _outputStyle;

        [MenuItem("Tools/BalloonParty/Upload Release")]
        private static void Open()
        {
            var window = GetWindow<ReleaseUploadWindow>("Upload Release");
            window.minSize = new Vector2(520, 420);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GitHub Release Upload", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds three APKs (release, release+cheats, development), " +
                "tags the commit, and uploads them to GitHub.\n\n" +
                "The working tree must be clean — no uncommitted changes allowed.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            DrawTokenField();
            EditorGUILayout.Space(8);

            DrawVersionField();
            DrawSummaryField();
            EditorGUILayout.Space(12);

            DrawUploadButton();
            EditorGUILayout.Space(8);

            DrawOutputLog();
        }

        private void DrawTokenField()
        {
            EditorGUILayout.LabelField("GitHub Token", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();

            var token = EditorPrefs.GetString(TokenPrefKey, "");
            var newToken = showToken
                ? EditorGUILayout.TextField(token)
                : EditorGUILayout.PasswordField(token);

            if (newToken != token)
            {
                EditorPrefs.SetString(TokenPrefKey, newToken.Trim());
            }

            if (GUILayout.Button(showToken ? "Hide" : "Show", GUILayout.Width(50)))
            {
                showToken = !showToken;
            }

            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrWhiteSpace(token))
            {
                EditorGUILayout.HelpBox(
                    "A fine-grained GitHub token with Contents read/write on this repo is required.\n" +
                    "Create one at: github.com/settings/tokens",
                    MessageType.Warning);
            }
        }

        private void DrawVersionField()
        {
            EditorGUILayout.LabelField("Version (e.g. 1.2.0)", EditorStyles.miniLabel);
            version = EditorGUILayout.TextField(version);
        }

        private void DrawSummaryField()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Release Summary (shown above changelog)", EditorStyles.miniLabel);
            summary = EditorGUILayout.TextArea(summary, GUILayout.Height(60));
        }

        private void DrawUploadButton()
        {
            EditorGUI.BeginDisabledGroup(isRunning);

            if (GUILayout.Button(
                    isRunning ? "Building & Uploading..." : "Build & Upload Release",
                    GUILayout.Height(36)))
            {
                ExecuteRelease();
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawOutputLog()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.miniLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

            _outputStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            EditorGUILayout.TextArea(outputLog, _outputStyle, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();
        }

        private void ExecuteRelease()
        {
            outputLog = "";

            var token = EditorPrefs.GetString(TokenPrefKey, "").Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                outputLog = "ERROR: GitHub token is not set.";
                return;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                outputLog = "ERROR: Version is required.";
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // --- Provenance: clean working tree ---
            var dirtyFiles = RunGit(projectRoot, "status --porcelain");
            if (!string.IsNullOrWhiteSpace(dirtyFiles))
            {
                outputLog = "ERROR: Working tree is dirty. Commit or stash all changes first.\n\n" +
                            dirtyFiles;
                return;
            }

            var commitSha = RunGit(projectRoot, "rev-parse HEAD").Trim();
            var treeHash = RunGit(projectRoot, "rev-parse HEAD^{tree}").Trim();
            AppendLog($"Commit:    {commitSha}");
            AppendLog($"Tree hash: {treeHash}");

            if (!EditorUtility.DisplayDialog(
                    "Confirm Release",
                    $"This will:\n" +
                    $"\u2022 Build 3 APKs (release, release+cheats, dev)\n" +
                    $"\u2022 Set version to {version}\n" +
                    $"\u2022 Create git tag v{version}\n" +
                    $"\u2022 Upload to GitHub\n\n" +
                    $"Commit: {commitSha[..10]}\n" +
                    $"Tree:   {treeHash[..10]}\n\n" +
                    $"Continue?",
                    "Build & Upload", "Cancel"))
            {
                return;
            }

            isRunning = true;
            Repaint();

            var builtApks = new List<string>();

            try
            {
                var outputDir = Path.Combine(projectRoot, BuildFolder, version);
                Directory.CreateDirectory(outputDir);

                var scenes = GetBuildScenes();
                if (scenes.Length == 0)
                {
                    outputLog = "ERROR: No scenes enabled in Build Settings.";
                    return;
                }

                PlayerSettings.bundleVersion = version;
                AppendLog($"Set bundle version to {version}");

                // --- Build 1: Release ---
                var releasePath = Path.Combine(outputDir, $"BalloonParty-{version}-release.apk");
                if (!BuildApk("Release", releasePath, scenes, BuildOptions.CompressWithLz4HC,
                        false, version, commitSha, treeHash, "release"))
                {
                    return;
                }

                builtApks.Add(releasePath);

                // --- Build 2: Release + Cheats ---
                var cheatsPath = Path.Combine(outputDir, $"BalloonParty-{version}-release-cheats.apk");
                if (!BuildApk("Release+Cheats", cheatsPath, scenes, BuildOptions.CompressWithLz4HC,
                        true, version, commitSha, treeHash, "release-cheats"))
                {
                    return;
                }

                builtApks.Add(cheatsPath);

                // --- Build 3: Development ---
                var devPath = Path.Combine(outputDir, $"BalloonParty-{version}-dev.apk");
                var devOptions = BuildOptions.Development | BuildOptions.AllowDebugging |
                                 BuildOptions.CompressWithLz4;
                if (!BuildApk("Development", devPath, scenes, devOptions,
                        false, version, commitSha, treeHash, "development"))
                {
                    return;
                }

                builtApks.Add(devPath);

                // --- SHA-256 checksums ---
                AppendLog("\n--- Checksums ---");
                var checksumLines = new List<string>();
                foreach (var apk in builtApks)
                {
                    var hash = ComputeSha256(apk);
                    var name = Path.GetFileName(apk);
                    checksumLines.Add($"{hash}  {name}");
                    AppendLog($"  {hash}  {name}");
                }

                // --- Upload ---
                AppendLog("\nAll builds succeeded. Starting upload...\n");
                Repaint();

                var scriptPath = Path.Combine(projectRoot, "Tools", "upload_release.sh");
                if (!File.Exists(scriptPath))
                {
                    AppendLog("ERROR: Tools/upload_release.sh not found.");
                    return;
                }

                var escapedSummary = summary.Replace("\"", "\\\"");
                var checksumArg = string.Join("\\n", checksumLines);
                var apkArgs = string.Join(" ", builtApks.Select(p => $"\"{p}\""));
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments =
                        $"\"{scriptPath}\" \"{version}\" \"{token}\" \"{Repository}\" " +
                        $"\"{escapedSummary}\" \"{checksumArg}\" {apkArgs}",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    AppendLog("ERROR: Failed to start upload process.");
                    return;
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                AppendLog(stdout);
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    AppendLog($"\n--- stderr ---\n{stderr}");
                }

                if (process.ExitCode == 0)
                {
                    AppendLog("\n\u2713 Release uploaded successfully!");
                    Debug.Log($"[Release] v{version} published to GitHub with 3 APKs.");
                }
                else
                {
                    AppendLog($"\n\u2717 Upload failed (exit code {process.ExitCode}).");
                    Debug.LogError($"[Release] Upload failed for v{version}.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"\nEXCEPTION: {ex.Message}\n{ex.StackTrace}");
                Debug.LogException(ex);
            }
            finally
            {
                SetCheatsDefine(false);
                CleanupBuildInfo(projectRoot);
                isRunning = false;
                Repaint();
            }
        }

        private bool BuildApk(
            string label,
            string outputPath,
            string[] scenes,
            BuildOptions options,
            bool addCheatsDefine,
            string ver,
            string commitSha,
            string treeHash,
            string variant)
        {
            AppendLog($"\n--- Building {label} ---");
            AppendLog($"  Output: {outputPath}");
            Repaint();

            SetCheatsDefine(addCheatsDefine);
            WriteBuildInfo(ver, commitSha, treeHash, variant);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = options
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                AppendLog($"ERROR: {label} build failed \u2014 {report.summary.totalErrors} error(s).");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error)
                        {
                            AppendLog($"  {msg.content}");
                        }
                    }
                }

                return false;
            }

            var size = new FileInfo(outputPath).Length / (1024f * 1024f);
            AppendLog($"  \u2713 {label} built ({size:F1} MB)");
            return true;
        }

        private static void WriteBuildInfo(string ver, string commitSha, string treeHash, string variant)
        {
            var dir = Path.GetDirectoryName(BuildInfoPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonUtility.ToJson(new BuildInfoData
            {
                version = ver,
                commitSha = commitSha,
                treeHash = treeHash,
                buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                variant = variant
            }, true);

            File.WriteAllText(BuildInfoPath, json);
            AssetDatabase.ImportAsset(BuildInfoPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CleanupBuildInfo(string projectRoot)
        {
            var fullPath = Path.Combine(projectRoot, BuildInfoPath);
            if (File.Exists(fullPath))
            {
                AssetDatabase.DeleteAsset(BuildInfoPath);
            }
        }

        private static void SetCheatsDefine(bool enable)
        {
            var target = NamedBuildTarget.Android;
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var hasCheats = defines.Contains(CheatsDefine);

            if (enable && !hasCheats)
            {
                defines.Add(CheatsDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
            else if (!enable && hasCheats)
            {
                defines.Remove(CheatsDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
        }

        private static string[] GetBuildScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        private static string RunGit(string workingDir, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return "";
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private void AppendLog(string text)
        {
            outputLog += text + "\n";
        }

        [Serializable]
        private class BuildInfoData
        {
            public string version;
            public string commitSha;
            public string treeHash;
            public string buildTime;
            public string variant;
        }
    }
}

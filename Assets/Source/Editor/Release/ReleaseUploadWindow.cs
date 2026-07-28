using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BalloonParty.Editor.Release
{
    /// <summary>
    ///     Editor window that automates GitHub release uploads for APK builds.
    ///     Creates a git tag, generates a changelog, and uploads the APK via the GitHub API.
    /// </summary>
    internal sealed class ReleaseUploadWindow : EditorWindow
    {
        private const string TokenPrefKey = "BalloonParty_GitHubToken";
        private const string ApkFolderPrefKey = "BalloonParty_ApkFolder";
        private const string DefaultApkFolder = "Builds";
        private const string Repository = "jose-villegas/BalloonParty";

        [SerializeField] private string version = "";
        [SerializeField] private string apkPath = "";

        private string outputLog = "";
        private Vector2 scrollPos;
        private bool isRunning;
        private bool showToken;

        [MenuItem("Tools/BalloonParty/Upload Release")]
        private static void Open()
        {
            var window = GetWindow<ReleaseUploadWindow>("Upload Release");
            window.minSize = new Vector2(500, 400);
            window.AutoDetectApk();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GitHub Release Upload", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawTokenField();
            EditorGUILayout.Space(8);

            DrawVersionField();
            DrawApkField();
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
            if (showToken)
            {
                var newToken = EditorGUILayout.TextField(token);
                if (newToken != token)
                {
                    EditorPrefs.SetString(TokenPrefKey, newToken.Trim());
                }
            }
            else
            {
                var newToken = EditorGUILayout.PasswordField(token);
                if (newToken != token)
                {
                    EditorPrefs.SetString(TokenPrefKey, newToken.Trim());
                }
            }

            if (GUILayout.Button(showToken ? "Hide" : "Show", GUILayout.Width(50)))
            {
                showToken = !showToken;
            }

            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrWhiteSpace(token))
            {
                EditorGUILayout.HelpBox(
                    "A GitHub Personal Access Token with 'repo' scope is required. " +
                    "Create one at: github.com/settings/tokens",
                    MessageType.Warning);
            }
        }

        private void DrawVersionField()
        {
            EditorGUILayout.LabelField("Version (e.g. 1.2.0)", EditorStyles.miniLabel);
            version = EditorGUILayout.TextField(version);
        }

        private void DrawApkField()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("APK Path", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            apkPath = EditorGUILayout.TextField(apkPath);

            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var folder = string.IsNullOrWhiteSpace(apkPath)
                    ? Path.Combine(Application.dataPath, "..", DefaultApkFolder)
                    : Path.GetDirectoryName(apkPath);
                var selected = EditorUtility.OpenFilePanel("Select APK", folder ?? "", "apk");
                if (!string.IsNullOrEmpty(selected))
                {
                    apkPath = selected;
                }
            }

            if (GUILayout.Button("Auto", GUILayout.Width(50)))
            {
                AutoDetectApk();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(apkPath) && File.Exists(apkPath))
            {
                var size = new FileInfo(apkPath).Length / (1024f * 1024f);
                EditorGUILayout.LabelField($"  Size: {size:F1} MB", EditorStyles.miniLabel);
            }
        }

        private void DrawUploadButton()
        {
            EditorGUI.BeginDisabledGroup(isRunning);
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            if (GUILayout.Button(isRunning ? "Uploading..." : "Upload Release", buttonStyle))
            {
                ExecuteRelease();
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawOutputLog()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.miniLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(outputLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void AutoDetectApk()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var buildsDir = Path.Combine(projectRoot, DefaultApkFolder);

            if (!Directory.Exists(buildsDir))
            {
                return;
            }

            var apks = Directory.GetFiles(buildsDir, "*.apk", SearchOption.AllDirectories);
            if (apks.Length == 0)
            {
                return;
            }

            // Pick the most recently modified APK
            var newest = apks[0];
            var newestTime = File.GetLastWriteTimeUtc(newest);
            for (int i = 1; i < apks.Length; i++)
            {
                var time = File.GetLastWriteTimeUtc(apks[i]);
                if (time > newestTime)
                {
                    newest = apks[i];
                    newestTime = time;
                }
            }

            apkPath = Path.GetFullPath(newest);
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

            if (string.IsNullOrWhiteSpace(apkPath) || !File.Exists(apkPath))
            {
                outputLog = "ERROR: APK file not found. Build the APK first or use Browse.";
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var scriptPath = Path.Combine(projectRoot, "Tools", "upload_release.sh");

            if (!File.Exists(scriptPath))
            {
                outputLog = "ERROR: Tools/upload_release.sh not found.";
                return;
            }

            isRunning = true;
            outputLog = "Starting release upload...\n";
            Repaint();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{scriptPath}\" \"{version}\" \"{apkPath}\" \"{token}\" \"{Repository}\"",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    outputLog += "ERROR: Failed to start process.";
                    return;
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                outputLog += stdout;
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    outputLog += "\n--- stderr ---\n" + stderr;
                }

                if (process.ExitCode == 0)
                {
                    outputLog += "\n\n✓ Release uploaded successfully!";
                    Debug.Log($"[Release] v{version} published to GitHub.");
                }
                else
                {
                    outputLog += $"\n\n✗ Release failed (exit code {process.ExitCode}).";
                    Debug.LogError($"[Release] Upload failed for v{version}. Check the window for details.");
                }
            }
            catch (Exception ex)
            {
                outputLog += $"\nEXCEPTION: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                isRunning = false;
                Repaint();
            }
        }
    }
}

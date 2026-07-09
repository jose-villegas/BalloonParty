using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BalloonParty.Editor.TestRunner
{
    /// <summary>
    ///     Runs the EditMode suite inside the open editor (no batchmode project-lock) and writes the
    ///     same compact <c>Tools/last-test-run.md</c> the headless runner produces, tagged with the
    ///     git HEAD it ran against so the pre-push hook can judge freshness.
    /// </summary>
    internal static class EditModeTestRunner
    {
        private const int MaxMessageLines = 2;

        private static TestRunnerApi _api;

        [MenuItem("Tools/BalloonParty/Run EditMode Tests")]
        internal static void Run()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new ResultWriter());
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            Debug.Log("[EditModeTestRunner] Running EditMode tests — result → Tools/last-test-run.md");
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var root = Path.GetDirectoryName(Application.dataPath);
                var failures = new List<ITestResultAdaptor>();
                CollectFailedLeaves(result, failures);

                var outcome = result.FailCount > 0 || result.TestStatus == TestStatus.Failed ? "FAILED" : "PASSED";
                var report = new StringBuilder();
                report.AppendLine($"# EditMode tests — {outcome}");
                report.AppendLine();
                report.AppendLine(
                    $"total {result.PassCount + result.FailCount + result.SkipCount} · " +
                    $"passed {result.PassCount} · failed {result.FailCount} · " +
                    $"skipped {result.SkipCount} · {result.Duration:0.0}s");

                if (failures.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("## Failures");
                    report.AppendLine();
                    foreach (var failure in failures)
                    {
                        report.AppendLine($"- {failure.FullName}");
                        foreach (var line in FirstLines(failure.Message, MaxMessageLines))
                        {
                            report.AppendLine($"    {line}");
                        }
                    }
                }

                report.AppendLine();
                report.AppendLine($"<!-- ran-against: {GitHead(root)} -->");

                File.WriteAllText(Path.Combine(root, "Tools", "last-test-run.md"), report.ToString());
                Debug.Log($"[EditModeTestRunner] {outcome} — {result.PassCount} passed, {result.FailCount} failed.");

                _api = null;
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            private static void CollectFailedLeaves(ITestResultAdaptor node, List<ITestResultAdaptor> into)
            {
                if (node.HasChildren)
                {
                    foreach (var child in node.Children)
                    {
                        CollectFailedLeaves(child, into);
                    }
                }
                else if (node.TestStatus == TestStatus.Failed)
                {
                    into.Add(node);
                }
            }

            private static IEnumerable<string> FirstLines(string text, int count)
            {
                if (string.IsNullOrEmpty(text))
                {
                    yield break;
                }

                var yielded = 0;
                foreach (var raw in text.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    yield return line;
                    if (++yielded >= count)
                    {
                        yield break;
                    }
                }
            }

            // Records the commit the run covered so the pre-push hook can gate on freshness.
            private static string GitHead(string root)
            {
                try
                {
                    var info = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                    {
                        WorkingDirectory = root,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = System.Diagnostics.Process.Start(info);
                    var sha = process.StandardOutput.ReadLine();
                    process.WaitForExit(2000);
                    return string.IsNullOrEmpty(sha) ? "unknown" : sha.Trim();
                }
                catch
                {
                    return "unknown";
                }
            }
        }
    }
}

// Builds one copy of the game for Tools/playtest/device_copies.py: a release APK with cheats whose app id is
// the project's plus @SUFFIX@, so several copies install side by side, and which keeps playing while another
// window has the focus. Send it to the Editor with `unity command script eval --file` after replacing
// @SUFFIX@ (for example .n1) and @OUT@ (the APK's path). The Editor must hold the keystore passwords and be
// out of Play mode. Every setting it changes is put back when the build ends.
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

const string Cheats = "CHEATS_IN_RELEASE";
var target = NamedBuildTarget.Android;
if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
{
    return "active target is " + EditorUserBuildSettings.activeBuildTarget + ", not Android; not switching";
}

var originalDefines = PlayerSettings.GetScriptingDefineSymbols(target);
var originalVariant = PlayerSettings.GetManagedCodeVariant(target);
var originalId = PlayerSettings.GetApplicationIdentifier(target);
var originalFocus = PlayerSettings.Android.runWithoutFocus;
try
{
    var defines = originalDefines.Split(';').Where(d => d.Length > 0).ToList();
    if (!defines.Contains(Cheats))
    {
        defines.Add(Cheats);
    }

    PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
    PlayerSettings.SetManagedCodeVariant(target, ManagedCodeVariant.Release);
    PlayerSettings.SetApplicationIdentifier(target, originalId + "@SUFFIX@");
    // Several copies play side by side in freeform windows; without this each pauses when another has the focus.
    PlayerSettings.Android.runWithoutFocus = true;
    var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
    {
        scenes = scenes,
        locationPathName = "@OUT@",
        target = BuildTarget.Android,
        options = BuildOptions.CompressWithLz4HC
    });

    var errors = string.Join("\n", report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error)
        .Select(m => m.content).Take(10));
    return report.summary.result + " in " + report.summary.totalTime + ", " +
           (report.summary.totalSize / (1024f * 1024f)).ToString("F1") + " MB\n" + errors;
}
finally
{
    PlayerSettings.SetScriptingDefineSymbols(target, originalDefines);
    PlayerSettings.SetManagedCodeVariant(target, originalVariant);
    PlayerSettings.SetApplicationIdentifier(target, originalId);
    PlayerSettings.Android.runWithoutFocus = originalFocus;
    AssetDatabase.SaveAssets();
}

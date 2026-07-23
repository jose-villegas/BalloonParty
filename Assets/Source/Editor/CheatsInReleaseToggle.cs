using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Menu toggle for the <c>CHEATS_IN_RELEASE</c> scripting define on the active build target.
    ///     While checked, release (non-development) builds compile the cheat console in — dev builds
    ///     and the editor always have it. The define persists in ProjectSettings until toggled off,
    ///     so turn it off before building anything meant to ship.
    /// </summary>
    internal static class CheatsInReleaseToggle
    {
        private const string Define = "CHEATS_IN_RELEASE";
        private const string MenuPath = "Tools/BalloonParty/Cheats In Release Builds";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            var target = ActiveTarget();
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (defines.Contains(Define))
            {
                defines.Remove(Define);
                Debug.Log($"{Define} removed for {target.TargetName} — release builds strip cheats again.");
            }
            else
            {
                defines.Add(Define);
                Debug.LogWarning($"{Define} added for {target.TargetName} — release builds now include the " +
                                 "cheat console. Toggle off before building for store submission.");
            }

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            var enabled = PlayerSettings.GetScriptingDefineSymbols(ActiveTarget())
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Contains(Define);
            Menu.SetChecked(MenuPath, enabled);
            return true;
        }

        private static NamedBuildTarget ActiveTarget()
        {
            return NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
        }
    }
}

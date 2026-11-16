using System.Reflection;
using BalloonParty.Configuration.Effects;
using BalloonParty.Display;
using BalloonParty.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.Display
{
    /// <summary>
    ///     Pins the wiring of the shipped effects clearing. It lives in serialized assets that no compile checks:
    ///     a dropped shader reference falls back to <c>Shader.Find</c>, which device builds strip, and a missing
    ///     component or registration leaves effects uncleared with nothing but a log line.
    /// </summary>
    [TestFixture]
    public class ShippedEffectsClearingTests
    {
        private const string SettingsPath = "Assets/Configuration/EffectsClearingSettings.asset";
        private const string CameraPath = "Assets/Prefabs/Game/Main Camera.prefab";
        private const string AppScopePath = "Assets/Prefabs/Game/AppLifetimeScope.prefab";

        [Test]
        public void Settings_HaveBothShadersAssigned()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EffectsClearingSettings>(SettingsPath);
            Assert.IsNotNull(settings, $"{SettingsPath} should load");

            Assert.IsNotNull(settings.MaskShader, "mask shader should be assigned so builds keep it");
            Assert.AreEqual("Hidden/BalloonParty/Display/EffectsClearingMask", settings.MaskShader.name);
            Assert.IsNotNull(settings.CompositeShader, "composite shader should be assigned so builds keep it");
            Assert.AreEqual("BalloonParty/Display/EffectsMapComposite", settings.CompositeShader.name);
        }

        [Test]
        public void MainCameraPrefab_CarriesTheMaskViewAndTheCompositor()
        {
            var camera = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            Assert.IsNotNull(camera, $"{CameraPath} should load");

            Assert.IsNotNull(camera.GetComponentInChildren<EffectsClearingMaskView>(true),
                "the controller binds to this view; without it the scope fails to resolve");
            Assert.IsNotNull(camera.GetComponentInChildren<EffectsMapCompositor>(true),
                "without the compositor the effects never lay back over the frame");
        }

        [Test]
        public void AppLifetimeScopePrefab_ReferencesTheSettings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppScopePath);
            Assert.IsNotNull(prefab, $"{AppScopePath} should load");
            var scope = prefab.GetComponent<AppLifetimeScope>();
            Assert.IsNotNull(scope);

            var field = typeof(AppLifetimeScope).GetField("_effectsClearingSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "AppLifetimeScope._effectsClearingSettings was renamed; update this test");
            Assert.IsNotNull((Object)field.GetValue(scope),
                "an unassigned settings asset registers null and breaks the mask view's injection");
        }
    }
}

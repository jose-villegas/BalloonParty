using System.Reflection;
using BalloonParty.Configuration.Balloons;
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
    ///     component or registration leaves effects uncleared with nothing but a log line, and a pop, item effect or nuke
    ///     cloud left on a board layer isn't cleared at all.
    /// </summary>
    [TestFixture]
    public class ShippedEffectsClearingTests
    {
        private const string SettingsPath = "Assets/Configuration/EffectsClearingSettings.asset";
        private const string CameraPath = "Assets/Prefabs/Game/Main Camera.prefab";
        private const string AppScopePath = "Assets/Prefabs/Game/AppLifetimeScope.prefab";
        private const string PopEffectsFolder = "Assets/Prefabs/Balloon/VFX";
        private const string ItemEffectsFolder = "Assets/Prefabs/Items/BalloonItems/VFX";
        private const string BalloonsConfigPath = "Assets/Configuration/BalloonsConfiguration.asset";

        // The shield item's gain burst is the ball's own news, not chaos around it, so it stays on the board.
        private static readonly string[] BoardEffects = { "PSVFX_ShieldGainPU" };
        private const string LegacyParticleShader = "Mobile/Particles/Alpha Blended";

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

        [Test]
        public void PopEffects_DrawIntoTheHudMap()
        {
            AssertDrawIntoTheHudMap(PopEffectsFolder);
        }

        [Test]
        public void ItemEffects_DrawIntoTheHudMap()
        {
            AssertDrawIntoTheHudMap(ItemEffectsFolder);
        }

        [Test]
        public void NukeCloud_DrawsIntoTheHudMap()
        {
            var config = AssetDatabase.LoadAssetAtPath<BalloonsConfiguration>(BalloonsConfigPath);
            Assert.IsNotNull(config, $"{BalloonsConfigPath} should load");
            var layer = LayerMask.NameToLayer(config.NukeBlast.CloudLayerName);
            Assert.GreaterOrEqual(layer, 0, $"no physics layer '{config.NukeBlast.CloudLayerName}'");

            Assert.AreNotEqual(0, HudMapLayers() & (1 << layer),
                "the nuke cloud draws outside the HUD effects map, so the glass over a ball can't clear it");
        }

        private static int HudMapLayers()
        {
            var camera = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            Assert.IsNotNull(camera, $"{CameraPath} should load");
            var compositor = camera.GetComponentInChildren<EffectsMapCompositor>(true);
            Assert.IsNotNull(compositor);
            var field = typeof(EffectsMapCompositor).GetField("_effectLayers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "EffectsMapCompositor._effectLayers was renamed; update this test");
            return ((LayerMask)field.GetValue(compositor)).value;
        }

        private static void AssertDrawIntoTheHudMap(string folder)
        {
            var mapLayers = HudMapLayers();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            Assert.IsNotEmpty(guids, $"no effects found under {folder}");
            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (System.Array.IndexOf(BoardEffects, prefab.name) >= 0)
                {
                    continue;
                }

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var name = $"{prefab.name}/{renderer.name}";
                    Assert.AreNotEqual(0, mapLayers & (1 << renderer.gameObject.layer),
                        $"{name} draws outside the HUD effects map, so the glass over a ball can't clear it");
                    foreach (var material in renderer.sharedMaterials)
                    {
                        // The legacy shader blends alpha into alpha, so in the map a shard would read half see-through.
                        Assert.IsFalse(material != null && material.shader.name == LegacyParticleShader,
                            $"{name}'s {material?.name} writes no coverage into the map");
                    }
                }
            }
        }
    }
}

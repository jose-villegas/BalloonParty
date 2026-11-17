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
    ///     cloud left on a board layer isn't cleared at all. One drawn by the HUD camera loses its bloom: the
    ///     lightning and the laser are made bright for it.
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
        private const string EffectsLayerName = "Effects";

        // The shield item's gain burst is the ball's own news, not chaos around it, so it stays on the board.
        private static readonly string[] BoardEffects = { "PSVFX_ShieldGainPU" };

        // The lightning's glow is a light on the ground, sorted under the balloons; the map, which lays
        // everything over them, would lift it over them.
        private static readonly string[] BoardRenderers = { "LVFX_LightningRange/Glow" };
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
        public void MainMap_IsHdr()
        {
            Assert.IsTrue((bool)CompositorField(MainCompositor(), "_hdr"),
                "an 8-bit main map clips the lightning and the laser to 1 under the glass, so bloom can't see them");
        }

        [Test]
        public void EffectsLayer_DrawsOnTheMainCameraUnderPost()
        {
            var layer = LayerMask.NameToLayer(EffectsLayerName);
            Assert.GreaterOrEqual(layer, 0, $"no physics layer '{EffectsLayerName}'; add it in the Editor");
            var camera = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);

            var reveal = camera.GetComponent<NavigationCameraReveal>();
            Assert.IsNotNull(reveal);
            var gameMask = (LayerMask)typeof(NavigationCameraReveal)
                .GetField("_gameMask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reveal);
            Assert.AreNotEqual(0, gameMask.value & (1 << layer), "the main camera never draws the effects");
            Assert.IsTrue(RendersPostProcessing(camera), "the main camera's post-processing is off");

            foreach (var other in camera.GetComponentsInChildren<Camera>(true))
            {
                if (other.gameObject != camera)
                {
                    Assert.AreEqual(0, other.cullingMask & (1 << layer),
                        $"{other.name} draws the effects after post: no bloom, no grading");
                }
            }
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
        public void PopEffects_DrawIntoTheMainMap()
        {
            AssertDrawIntoTheMainMap(PopEffectsFolder);
        }

        [Test]
        public void ItemEffects_DrawIntoTheMainMap()
        {
            AssertDrawIntoTheMainMap(ItemEffectsFolder);
        }

        [Test]
        public void NukeCloud_DrawsIntoTheMainMap()
        {
            var config = AssetDatabase.LoadAssetAtPath<BalloonsConfiguration>(BalloonsConfigPath);
            Assert.IsNotNull(config, $"{BalloonsConfigPath} should load");
            var layer = LayerMask.NameToLayer(config.NukeBlast.CloudLayerName);
            Assert.GreaterOrEqual(layer, 0, $"no physics layer '{config.NukeBlast.CloudLayerName}'");

            Assert.AreNotEqual(0, MainMapLayers() & (1 << layer),
                "the nuke cloud draws outside the main effects map, so the glass over a ball can't clear it");
        }

        // Through the serialized URP camera data: this assembly doesn't reference URP.
        private static bool RendersPostProcessing(GameObject camera)
        {
            foreach (var component in camera.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.GetType().Name == "UniversalAdditionalCameraData")
                {
                    return new SerializedObject(component).FindProperty("m_RenderPostProcessing").boolValue;
                }
            }

            Assert.Fail("the main camera has no URP camera data");
            return false;
        }

        private static EffectsMapCompositor MainCompositor()
        {
            var camera = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            Assert.IsNotNull(camera, $"{CameraPath} should load");
            var compositor = camera.GetComponent<EffectsMapCompositor>();
            Assert.IsNotNull(compositor, "the main camera has no effects map of its own");
            return compositor;
        }

        private static object CompositorField(EffectsMapCompositor compositor, string name)
        {
            var field = typeof(EffectsMapCompositor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"EffectsMapCompositor.{name} was renamed; update this test");
            return field.GetValue(compositor);
        }

        private static int MainMapLayers()
        {
            return ((LayerMask)CompositorField(MainCompositor(), "_effectLayers")).value;
        }

        private static void AssertDrawIntoTheMainMap(string folder)
        {
            var mapLayers = MainMapLayers();
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
                    if (System.Array.IndexOf(BoardRenderers, name) >= 0)
                    {
                        continue;
                    }

                    Assert.AreNotEqual(0, mapLayers & (1 << renderer.gameObject.layer),
                        $"{name} draws outside the main effects map, so the glass over a ball can't clear it");
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

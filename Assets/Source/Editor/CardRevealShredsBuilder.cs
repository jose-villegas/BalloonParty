using BalloonParty.Configuration.CardReveal;
using BalloonParty.Shared.Pool;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     The card reveal's shreds, part of <see cref="CardRevealBuilder" />: the burst of torn rubber the reveal
    ///     flings out as its balloon tears, flying far and fluttering down off the screen. Sets up the scraps'
    ///     texture (<c>Tools/art/balloon_pop/make_shreds.py</c>), and makes their lit material and the particle
    ///     prefab if they are missing, so a rebuild keeps their tuning; assigns the prefab on the settings.
    /// </summary>
    internal static class CardRevealShredsBuilder
    {
        private const string TexturePath = "Assets/Sprites/UI/CardReveal/shreds.png";
        private const string MaterialPath = "Assets/Materials/UI/CardRevealShred.mat";
        private const string PrefabPath = "Assets/Prefabs/Game/CardRevealShreds.prefab";
        private const string ShaderName = "BalloonParty/Sprite/Diffuse";
        private const string Layer = "Balloons";
        private const string SortingLayer = "Sky";

        // Under the reveal's canvas (31999): behind its balloon and ribbons, over the board, under the light.
        private const int SortingOrder = 31998;
        private const int SheetTiles = 2;

        private static readonly int AmbientHueId = Shader.PropertyToID("_AmbientHue");
        private static readonly int AmbientDimmingId = Shader.PropertyToID("_AmbientDimming");
        private static readonly int AmbientDesaturationId = Shader.PropertyToID("_AmbientDesaturation");

        /// <summary>Makes what is missing and assigns the prefab on <paramref name="settings" /> if it has none.</summary>
        internal static void Build(CardRevealSettings settings)
        {
            var texture = ImportTexture();
            if (texture == null)
            {
                Debug.LogWarning($"[CardReveal] No {TexturePath}; run Tools/art/balloon_pop/make_shreds.py. No shreds.");
                return;
            }

            var material = EnsureMaterial(texture);
            if (material == null)
            {
                Debug.LogError($"[CardReveal] No {ShaderName} shader; no shreds.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(PrefabPath) ?? CreatePrefab(material);
            var fields = new SerializedObject(settings);
            var field = fields.FindProperty("_shredsPrefab");
            if (field.objectReferenceValue == null)
            {
                field.objectReferenceValue = prefab;
                fields.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        private static Texture2D ImportTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            if (importer.textureType != TextureImporterType.Default || !importer.alphaIsTransparency ||
                importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        // In the scene's light as the reveal's rubber takes it (LitFlipbook's ambient defaults).
        private static Material EnsureMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader) { mainTexture = texture };
            material.SetFloat(AmbientHueId, 0.25f);
            material.SetFloat(AmbientDimmingId, 0.4f);
            material.SetFloat(AmbientDesaturationId, 0.5f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // In canvas units: the burst is parented to the reveal's canvas (225 wide), so it scales with the screen
        // and follows the camera. Flung out fast from the balloon's skin, slowed by the air, then fluttering down
        // under gravity, tumbling, on unscaled time like the reveal. A scrap's strip fills a part of its cell, so
        // the particles are larger than the strips look; they tip and rock rather than spin through edge-on,
        // where a flat scrap shows as a dark sliver.
        private static ParticleSystem CreatePrefab(Material material)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(PrefabPath))
            {
                layer = LayerMask.NameToLayer(Layer)
            };
            try
            {
                var particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                root.AddComponent<PoolableParticle>();

                var main = particles.main;
                main.duration = 1f;
                main.loop = false;
                main.playOnAwake = false;
                main.useUnscaledTime = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = 40;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(140f, 280f);
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(16f, 26f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(14f, 22f);
                main.startSizeZ = 1f;
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 0f;

                var emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16, 22) });

                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 28f;
                shape.radiusThickness = 0f;
                shape.arc = 360f;

                var gravity = particles.forceOverLifetime;
                gravity.enabled = true;
                gravity.space = ParticleSystemSimulationSpace.Local;
                gravity.x = 0f;
                gravity.y = -260f;
                gravity.z = 0f;

                var air = particles.limitVelocityOverLifetime;
                air.enabled = true;
                air.limit = 10000f;
                air.drag = 1.1f;
                air.multiplyDragByParticleSize = false;
                air.multiplyDragByParticleVelocity = false;

                var tumble = particles.rotationOverLifetime;
                tumble.enabled = true;
                tumble.separateAxes = true;
                tumble.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
                tumble.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
                tumble.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

                var fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;

                // One scrap of the four, at random, kept for its life.
                var sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = SheetTiles;
                sheet.numTilesY = SheetTiles;
                sheet.frameOverTime = 0f;
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, SheetTiles * SheetTiles - 0.01f);
                sheet.cycleCount = 1;

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sharedMaterial = material;
                renderer.sortingLayerName = SortingLayer;
                renderer.sortingOrder = SortingOrder;

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<ParticleSystem>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

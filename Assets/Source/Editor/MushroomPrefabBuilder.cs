using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;
using BalloonParty.EditorUI.Utilities;
using BalloonParty.Shared.Animation;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds everything a meadow mushroom needs to appear in play (@ref plan_meadow_mushrooms), or brings it
    ///     up to date: the material on the <c>Grid/Mushroom</c> shader, the quad the shader draws on, the
    ///     <c>Mushroom</c> prefab (a <see cref="MushroomView" /> and the quad's renderer, no collider), and the
    ///     <see cref="GridActorType.Mushroom" /> entry in the grid actor configuration, which is what makes the
    ///     spawner place them.
    /// </summary>
    /// <remarks>
    ///     Safe to run again: the material keeps the values tuned on it, the quad and the prefab keep their place
    ///     in the project, and the entry is added once and pointed at the prefab each time. It shows no dialog, so
    ///     a script can call it.
    /// </remarks>
    internal static class MushroomPrefabBuilder
    {
        private const string ShaderName = "BalloonParty/Grid/Mushroom";
        private const string MaterialPath = "Assets/Materials/Grid/Mushroom.mat";
        private const string PrefabFolder = "Assets/Prefabs/Grid/Mushroom";
        private const string PrefabPath = PrefabFolder + "/Mushroom.prefab";
        private const string QuadPath = PrefabFolder + "/MushroomQuad.asset";
        private const string SporeShaderName = "BalloonParty/Particle/SporeSpeck";
        private const string SporeMaterialPath = "Assets/Materials/Grid/MushroomSpore.mat";

        // The board's own layers. Where the bushes are, so the GI's scene capture reads the mushroom as an occluder
        // as it reads them. And at the bottom of the balloons' sorting layer: a mushroom grows from the ground, so
        // a balloon that overlaps a knocked cap, the balloons' shadows, a pop's burst, the ball and a lance's line
        // all draw over it and over the shadow it casts, and only the ground draws under.
        private const string Layer = "Grid";
        private const string SortingLayer = "Balloons";
        private const int SortingOrder = -10;

        // The spores are in the air: with the other effects, off the layer the GI reads the mushroom on as an
        // occluder, and drawn over the balloons, as a pop's own particles are.
        private const string SporeLayer = "Effects";
        private const int SporeSortingOrder = 200;

        // The most specks one mushroom has alive: a blast's burst, the few its flying cap trails, and a hit or
        // two that came just before.
        private const int MostSpecks = 128;

        // How fast a speck loses its speed, so one that leaves at a ball's pace has all but stopped in half a
        // second and hangs for the rest of its life; how fast it drifts up while it hangs, in world units a
        // second each second; and where in its life it has come in whole and starts to fade.
        private const float SpeckDrag = 3f;
        private const float SpeckLift = 0.35f;
        private const float SpeckFadeIn = 0.08f;
        private const float SpeckFadeFrom = 0.55f;
        private const float SpeckEndSize = 0.5f;

        // The material's swell at a full pulse, as a share of the radius: the quad is sized from it.
        private static readonly int PulseSwellId = Shader.PropertyToID("_PulseSwell");

        private static readonly EditorAssetCache<GridActorConfiguration> ConfigurationCache = new();
        private static readonly EditorAssetCache<LevelPacingConfiguration> PacingCache = new();

        [MenuItem("Tools/BalloonParty/Meadow/Build Mushroom Prefab")]
        private static void Build()
        {
            var settings = PacingCache.Value != null ? PacingCache.Value.Mushrooms : null;
            if (settings == null)
            {
                Debug.LogError("[MushroomPrefabBuilder] No Level Pacing asset to read the mushroom's size from.");
                return;
            }

            var material = EnsureMaterial(ShaderName, MaterialPath, "Mushroom");
            if (material == null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/Grid", "Mushroom");
            }

            var sporeMaterial = EnsureMaterial(SporeShaderName, SporeMaterialPath, "MushroomSpore");
            if (sporeMaterial == null)
            {
                return;
            }

            var view = BuildPrefab(material, EnsureQuad(settings, material), sporeMaterial);
            if (view == null)
            {
                return;
            }

            AddConfigurationEntry(view);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MushroomPrefabBuilder] Built {PrefabPath}.");
        }

        private static Material EnsureMaterial(string shaderName, string path, string name)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[MushroomPrefabBuilder] Shader {shaderName} was not found.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // MushroomView pushes a property block, so instancing would only drop what it pushes.
                material = new Material(shader) { name = name, enableInstancing = false };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // The shader draws the mushroom and its shadow from the quad's own position and reads no texture, so the
        // quad is all the geometry there is: a square about the slot's centre, with room for the cap to lean and
        // swell and for the shadow to fall beside it.
        private static Mesh EnsureQuad(IMushroomSettings settings, Material material)
        {
            // The most the mushroom can need at once, from the settings and the material's own swell
            // (MushroomQuadRoom, which a test holds the built quad to).
            var half = MushroomQuadRoom.Half(
                settings.Radius, material.GetFloat(PulseSwellId), settings.Lean, settings.Idle, settings.Cut);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);
            var isNew = mesh == null;
            if (isNew)
            {
                mesh = new Mesh { name = "MushroomQuad" };
            }

            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-half, -half, 0f), new Vector3(half, -half, 0f),
                new Vector3(half, half, 0f), new Vector3(-half, half, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, QuadPath);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        private static MushroomView BuildPrefab(Material material, Mesh quad, Material sporeMaterial)
        {
            // Built whole each time and saved over what is there: the asset keeps its place in the project, and
            // the configuration's entry is pointed at the new view below.
            var root = new GameObject("Mushroom") { layer = LayerMask.NameToLayer(Layer) };
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = quad;
                var body = root.AddComponent<MeshRenderer>();
                body.sharedMaterial = material;
                body.shadowCastingMode = ShadowCastingMode.Off;
                body.receiveShadows = false;
                body.lightProbeUsage = LightProbeUsage.Off;
                body.reflectionProbeUsage = ReflectionProbeUsage.Off;
                body.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                body.sortingLayerName = SortingLayer;
                body.sortingOrder = SortingOrder;

                var tweenTracker = root.AddComponent<TweenTracker>();
                var view = root.AddComponent<MushroomView>();

                // Off until its cap moves: the view switches itself on for a hit, a cut or a regrow and off
                // again at rest, and the prefab says so too, so one in a scene does not tick.
                view.enabled = false;
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_tweenTracker").objectReferenceValue = tweenTracker;
                serialized.FindProperty("_body").objectReferenceValue = body;
                serialized.FindProperty("_spores").objectReferenceValue = BuildSpores(root.transform, sporeMaterial);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved != null ? saved.GetComponent<MushroomView>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The mushroom's specks: one particle system, never playing by itself. MushroomView emits into it by
        // hand, one speck at a time with its own place, way, speed, size, life and colour; what the system adds
        // is that a speck slows, comes in and fades out, and shrinks.
        private static ParticleSystem BuildSpores(Transform mushroom, Material material)
        {
            var holder = new GameObject("Spores") { layer = LayerMask.NameToLayer(SporeLayer) };
            holder.transform.SetParent(mushroom, false);
            var spores = holder.AddComponent<ParticleSystem>();
            spores.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = spores.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MostSpecks;
            main.gravityModifier = 0f;
            main.startSpeed = 0f;

            var emission = spores.emission;
            emission.enabled = false;
            var shape = spores.shape;
            shape.enabled = false;

            var slowing = spores.limitVelocityOverLifetime;
            slowing.enabled = true;
            slowing.limit = float.MaxValue;
            slowing.dampen = 0f;
            slowing.drag = SpeckDrag;
            slowing.multiplyDragByParticleSize = false;
            slowing.multiplyDragByParticleVelocity = false;

            var lift = spores.forceOverLifetime;
            lift.enabled = true;
            lift.space = ParticleSystemSimulationSpace.World;
            lift.x = 0f;
            lift.y = SpeckLift;
            lift.z = 0f;

            var fade = spores.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, SpeckFadeIn),
                    new GradientAlphaKey(1f, SpeckFadeFrom), new GradientAlphaKey(0f, 1f)
                });
            fade.color = gradient;

            var shrink = spores.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, SpeckEndSize));

            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = SporeSortingOrder;
            return spores;
        }

        private static void AddConfigurationEntry(MushroomView prefab)
        {
            var configuration = ConfigurationCache.Value;
            if (configuration == null)
            {
                Debug.LogError("[MushroomPrefabBuilder] No GridActorConfiguration asset: the prefab was built, but no " +
                               "level will place it.");
                return;
            }

            var serialized = new SerializedObject(configuration);
            var entries = serialized.FindProperty("_entries");
            var entry = FindEntry(entries);
            if (entry == null)
            {
                entries.arraySize++;
                entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);

                // The mushrooms' slots are the wall catalogue's, whatever this says; a mushroom is never hit.
                entry.FindPropertyRelative("_placementMode").intValue = (int)SlotPlacementMode.Random;
                entry.FindPropertyRelative("_hitsToPop").intValue = -1;
            }

            entry.FindPropertyRelative("_actorType").intValue = (int)GridActorType.Mushroom;
            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(configuration);
            }
        }

        private static SerializedProperty FindEntry(SerializedProperty entries)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_actorType").intValue == (int)GridActorType.Mushroom)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}

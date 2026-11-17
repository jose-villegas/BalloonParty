using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Says whether anything is alive on a layer, so a camera that renders nothing but that layer (an effects
    ///     map) can skip the frames it would render empty. Every pooled object with a renderer is tracked from the
    ///     moment its pool creates it, and counts on its renderers' layers while it's active: pooled effects live
    ///     exactly as long as they're out of the pool. A persistent object that shows and hides by its renderer
    ///     instead (the nuke cloud) is watched by that renderer.
    /// </summary>
    internal static class LayerPresence
    {
        private const int LayerCount = 32;

        private static readonly int[] Counts = new int[LayerCount];
        private static readonly List<Renderer> Watched = new();
        private static readonly List<Renderer> Scratch = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            System.Array.Clear(Counts, 0, LayerCount);
            Watched.Clear();
        }

        /// <summary>True while a tracked object is active, or a watched renderer is shown, on any of the layers.</summary>
        internal static bool Any(int layerMask)
        {
            for (var mask = layerMask; mask != 0; mask &= mask - 1)
            {
                if (Counts[LowestLayer(mask)] > 0)
                {
                    return true;
                }
            }

            for (var i = Watched.Count - 1; i >= 0; i--)
            {
                var watched = Watched[i];
                if (watched == null)
                {
                    Watched.RemoveAt(i);
                }
                else if ((layerMask & (1 << watched.gameObject.layer)) != 0 && watched.enabled
                         && watched.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Counts <paramref name="root" /> on its renderers' layers while it's active. Once per object: its pool
        ///     calls it as it creates the object, so the layers are the prefab's.
        /// </summary>
        internal static void Track(GameObject root)
        {
            if (root.TryGetComponent<LayerPresenceTag>(out _))
            {
                return;
            }

            root.GetComponentsInChildren(true, Scratch);
            var layers = 0;
            foreach (var renderer in Scratch)
            {
                layers |= 1 << renderer.gameObject.layer;
            }

            Scratch.Clear();
            if (layers != 0)
            {
                root.AddComponent<LayerPresenceTag>().Layers = layers;
            }
        }

        /// <summary>Counts <paramref name="renderer" /> on its layer while it's enabled and active, until it's destroyed.</summary>
        internal static void Watch(Renderer renderer)
        {
            if (renderer != null && !Watched.Contains(renderer))
            {
                Watched.Add(renderer);
            }
        }

        internal static void Add(int layers, int delta)
        {
            for (var mask = layers; mask != 0; mask &= mask - 1)
            {
                Counts[LowestLayer(mask)] += delta;
            }
        }

#if UNITY_ENABLE_CHECKS
        /// <summary>
        ///     A renderer drawing on the layers while <see cref="Any" /> says nothing is there: a source neither
        ///     pooled nor watched, which an effects map skipping its frames would leave out of the glass.
        /// </summary>
        internal static Renderer FindUntracked(int layerMask)
        {
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if ((layerMask & (1 << renderer.gameObject.layer)) != 0 && renderer.enabled && Draws(renderer))
                {
                    return renderer;
                }
            }

            return null;
        }

        private static bool Draws(Renderer renderer)
        {
            return renderer switch
            {
                ParticleSystemRenderer => renderer.TryGetComponent<ParticleSystem>(out var particles)
                                          && particles.particleCount > 0,
                TrailRenderer trail => trail.positionCount > 0,
                LineRenderer line => line.positionCount > 0,
                SpriteRenderer sprite => sprite.sprite != null && sprite.color.a > 0f,
                _ => true
            };
        }
#endif

        private static int LowestLayer(int mask)
        {
            var layer = 0;
            while ((mask & (1 << layer)) == 0)
            {
                layer++;
            }

            return layer;
        }
    }
}

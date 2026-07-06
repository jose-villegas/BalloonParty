using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BalloonParty.Shared.Extensions
{
    internal static class SceneExtensions
    {
        /// <summary>Disables all enabled Cameras, Canvases, AudioListeners and EventSystems; returns a handle to restore them.</summary>
        public static SceneRenderingHandle SuppressRendering(this Scene scene)
        {
            var handle = new SceneRenderingHandle();

            foreach (var root in scene.GetRootGameObjects())
            {
                Collect(root, handle.Cameras);
                Collect(root, handle.Canvases);
                Collect(root, handle.Listeners);
                Collect(root, handle.EventSystems);
            }

            return handle;
        }

        private static void Collect<T>(GameObject root, List<T> targets)
            where T : Behaviour
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                if (component.enabled)
                {
                    component.enabled = false;
                    targets.Add(component);
                }
            }
        }
    }

    internal class SceneRenderingHandle
    {
        internal readonly List<Camera> Cameras = new();
        internal readonly List<Canvas> Canvases = new();
        internal readonly List<AudioListener> Listeners = new();
        internal readonly List<EventSystem> EventSystems = new();

        public void Restore()
        {
            Restore(Cameras);
            Restore(Canvases);
            Restore(Listeners);
            Restore(EventSystems);
        }

        private static void Restore<T>(List<T> targets)
            where T : Behaviour
        {
            foreach (var component in targets)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }

            targets.Clear();
        }
    }
}

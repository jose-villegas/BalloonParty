#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor.Staging
{
    /// <summary>
    ///     Redraws the frame the stage holds one draw group at a time, back to front, so the pictures show what
    ///     each group adds: the backdrop, the specks over it, the balloons' shadows, their bodies, their shines,
    ///     the rest of the board, the effects, the light laid over it all, and the post-processing. What the HUD's camera draws
    ///     is in none of them: the Game view's own picture is the last step. For the frame composition
    ///     document (<c>Docs/frame-composition.md</c>), driven by <c>Tools/frame/frame_doc.py</c> through the Unity
    ///     CLI. It only switches renderers (and the canvases drawn in the scene) off and on around renders of its
    ///     own, inside one call, and leaves every one as it found it; the game must be held
    ///     (<see cref="SceneStage.Hold" />), or what it hides would be missing from a frame the game draws.
    /// </summary>
    public static class FrameBreakdown
    {
        private const string BackdropSortingLayer = "Default";
        private const string BalloonsSortingLayer = "Balloons";
        private const string EffectsLayer = "Effects";
        private const string SpeckShader = "SpeckField";
        private const string FormationRingName = "FormationGlowQuad";
        private const string LightOverlayShader = "ScreenSpaceLightOverlay";
        private const string EffectsCompositeShader = "EffectsMapComposite";

        private static readonly int SceneLightColorId = Shader.PropertyToID("_SceneLightColor");
        private static readonly int SceneLightFieldOnId = Shader.PropertyToID("_SceneLightFieldOn");

        // In the order the main camera draws them: each picture holds its group and every one before it.
        private static readonly string[] Groups =
        {
            "backdrop", "specks", "shadows", "bodies", "shines", "board", "effects", "light"
        };

        // What only the HUD's camera draws (the interface, the score shapes and trails): after every group, and
        // in none of these pictures. Rendering the main camera by hand renders its stack too, so left on, a
        // score shape would show in the backdrop's picture, though the game draws it last of all.
        private static readonly int HudGroup = Groups.Length;

        /// <summary>
        ///     Writes the build-up into <paramref name="directory" />, one picture per group and named for it,
        ///     <c>backdrop.png</c> to <c>light.png</c>, each with the groups before it; <c>post.png</c> with the
        ///     post-processing on: the frame as the main camera hands it to the HUD; and <c>flat.png</c>, everything
        ///     drawn with the time of day's light white and the local lights off: the art alone.
        ///     With <paramref name="around" /> (a balloon type, "col,row", or a world point "x;y") each is a
        ///     <paramref name="size" />-pixel square centred there; without it, <paramref name="scale" /> below 1
        ///     writes whole frames that much smaller, and <paramref name="flat" /> off leaves that picture out, for
        ///     a run of every frame of a shot. Answers how many renderers each group holds.
        /// </summary>
        public static string Write(
            string directory, string around = "", int size = 480, float scale = 1f, bool flat = true)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return "error: no main camera";
            }

            Directory.CreateDirectory(directory);
            var drawn = Drawn(camera);
            var cameraData = camera.GetUniversalAdditionalCameraData();
            var hadPost = cameraData.renderPostProcessing;
            var lightColor = Shader.GetGlobalColor(SceneLightColorId);
            var fieldOn = Shader.GetGlobalFloat(SceneLightFieldOnId);
            var answer = new StringBuilder();
            try
            {
                cameraData.renderPostProcessing = false;
                for (var step = 0; step < Groups.Length; step++)
                {
                    var upTo = step;
                    var count = Show(drawn, group => group <= upTo, step);
                    SceneStage.Capture(PathOf(directory, Groups[step]), around, size, scale);
                    answer.Append(answer.Length > 0 ? "|" : "").Append(Groups[step]).Append('=').Append(count);
                }

                var lightGroup = Array.IndexOf(Groups, "light");
                if (flat)
                {
                    Show(drawn, group => group < lightGroup, lightGroup);
                    Shader.SetGlobalColor(SceneLightColorId, Color.white);
                    Shader.SetGlobalFloat(SceneLightFieldOnId, 0f);
                    SceneStage.Capture(PathOf(directory, "flat"), around, size, scale);
                    Shader.SetGlobalColor(SceneLightColorId, lightColor);
                    Shader.SetGlobalFloat(SceneLightFieldOnId, fieldOn);
                }

                Show(drawn, group => group <= lightGroup, lightGroup);
                cameraData.renderPostProcessing = true;
                SceneStage.Capture(PathOf(directory, "post"), around, size, scale);
            }
            finally
            {
                Show(drawn, _ => true, 0);
                cameraData.renderPostProcessing = hadPost;
                Shader.SetGlobalColor(SceneLightColorId, lightColor);
                Shader.SetGlobalFloat(SceneLightFieldOnId, fieldOn);
            }

            return answer.ToString();
        }

        /// <summary>
        ///     Asks for the Game view as the player sees it, HUD and all, written to <paramref name="path" /> at the
        ///     end of the next frame the Editor draws: a held game still draws frames, a paused Editor only when it
        ///     is stepped (<see cref="SceneStage.Step" /> takes one of its last frame).
        /// </summary>
        public static string Screenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            return $"asked for {path}";
        }

        // Every renderer the main camera or a camera stacked on it is drawing now, and every canvas the main camera
        // draws in the scene, with its group.
        private static List<(Component Part, int Group)> Drawn(Camera camera)
        {
            var effectsLayer = LayerMask.NameToLayer(EffectsLayer);
            var stackMask = 0;
            foreach (var overlay in camera.GetUniversalAdditionalCameraData().cameraStack)
            {
                stackMask |= overlay != null ? overlay.cullingMask : 0;
            }

            var drawn = new List<(Component Part, int Group)>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // The effects layer counts even when the lens has taken it off the camera's mask: its composite
                // quad draws it then, and the two belong to one group.
                var layer = 1 << renderer.gameObject.layer;
                if ((camera.cullingMask & layer) != 0 || renderer.gameObject.layer == effectsLayer)
                {
                    drawn.Add((renderer, GroupOf(renderer, effectsLayer)));
                }
                else if ((stackMask & layer) != 0)
                {
                    drawn.Add((renderer, HudGroup));
                }
            }

            // A canvas drawn in the scene (the card reveal's balloon and ribbons) is no renderer, so nothing above
            // finds it, and left alone it would be in the backdrop's picture though it is drawn over the balloons.
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.enabled || !canvas.gameObject.activeInHierarchy || !canvas.isRootCanvas)
                {
                    continue;
                }

                var inScene = canvas.renderMode == RenderMode.WorldSpace
                    ? (camera.cullingMask & (1 << canvas.gameObject.layer)) != 0
                    : canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == camera;
                if (inScene)
                {
                    drawn.Add((canvas, Array.IndexOf(
                        Groups, canvas.sortingLayerName == BackdropSortingLayer ? "backdrop" : "board")));
                }
            }

            return drawn;
        }

        // Switches each renderer on or off by its group, and answers how many are in the group counted.
        private static int Show(List<(Component Part, int Group)> drawn, Func<int, bool> shown, int counted)
        {
            var count = 0;
            foreach (var (part, group) in drawn)
            {
                if (part is Renderer renderer)
                {
                    renderer.enabled = shown(group);
                }
                else if (part is Canvas canvas)
                {
                    canvas.enabled = shown(group);
                }
                else
                {
                    continue;
                }

                count += group == counted ? 1 : 0;
            }

            return count;
        }

        private static string PathOf(string directory, string name)
        {
            return Path.Combine(directory, name + ".png");
        }

        // By what draws it and where it sorts: the two full-screen quads and the speck field by their shader, the
        // effects by their layer, the backdrop and the balloons by their sorting layer, a balloon's parts by their
        // names in its prefab (BakedShadow, Body, Knot, Shine). The specks sort last in the backdrop's layer, so
        // their own group comes right after it.
        private static int GroupOf(Renderer renderer, int effectsLayer)
        {
            var material = renderer.sharedMaterial;
            var shader = material != null && material.shader != null ? material.shader.name : string.Empty;
            if (shader.Contains(LightOverlayShader))
            {
                return Array.IndexOf(Groups, "light");
            }

            if (shader.Contains(EffectsCompositeShader))
            {
                return Array.IndexOf(Groups, "effects");
            }

            // The specks and the unbreakables' rings are on the effects layer for the lens's sake, but with no
            // ball in the air they are drawn where they sort: over the backdrop, and under the balloons.
            if (shader.Contains(SpeckShader))
            {
                return Array.IndexOf(Groups, "specks");
            }

            if (renderer.gameObject.name == FormationRingName)
            {
                return Array.IndexOf(Groups, "bodies");
            }

            if (renderer.gameObject.layer == effectsLayer)
            {
                return Array.IndexOf(Groups, "effects");
            }

            var sorting = renderer.sortingLayerName;
            if (sorting == BackdropSortingLayer)
            {
                return Array.IndexOf(Groups, "backdrop");
            }

            if (sorting != BalloonsSortingLayer)
            {
                return Array.IndexOf(Groups, "board");
            }

            var name = renderer.gameObject.name;
            if (name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Array.IndexOf(Groups, "shadows");
            }

            return name.StartsWith("Shine", StringComparison.OrdinalIgnoreCase)
                ? Array.IndexOf(Groups, "shines")
                : Array.IndexOf(Groups, "bodies");
        }
    }
}
#endif

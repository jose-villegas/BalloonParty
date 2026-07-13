using System;
using System.Linq;
using BalloonParty.Configuration.Palette;
using BalloonParty.Display;
using BalloonParty.Shared;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.Maps
{
    /// <summary>
    ///     Unified play-mode preview for the project's shared render-target "maps" — scene
    ///     capture, disturbance field, and the screen-space GI light buffer — with per-channel
    ///     RGBA isolation. Supersedes <c>DisturbanceFieldPreviewWindow</c>; the per-inspector
    ///     previews on <see cref="SceneCaptureService"/> and <see cref="ScreenSpaceLightService"/>
    ///     stay as-is (useful with that component selected) but this is the one-stop window.
    /// </summary>
    internal sealed class GameRenderMapsWindow : EditorWindow
    {
        private const string GenericChannelTooltipFormat =
            "Raw {0} channel — no defined meaning for this texture; assign anything to inspect it channel-by-channel.";

        private static readonly int SceneCaptureTexId = Shader.PropertyToID("_SceneCaptureTex");
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int DisturbanceColorTexId = Shader.PropertyToID("_DisturbanceColorTex");
        private static readonly int ChannelMaskId = Shader.PropertyToID("_ChannelMask");
        private static readonly int PaletteColorsId = Shader.PropertyToID("_PaletteColors");
        private static readonly int DecodePaletteId = Shader.PropertyToID("_DecodePalette");
        private static readonly string[] ChannelLabels = { "R", "G", "B", "A" };
        private static readonly MapDescriptor[] Descriptors = BuildDescriptors();
        private static readonly string[] MapNames = Descriptors.Select(d => d.Name).ToArray();

        private readonly bool[] _channelEnabled = { true, true, true, true };
        private readonly ConfigAssetCache<GamePalette> _paletteCache = new();
        private readonly Vector4[] _paletteBuffer = new Vector4[16];

        private Material _channelMaterial;
        private int _selectedIndex;
        private Texture _customTexture;
        private bool _decodePalette = true;

        [MenuItem("Tools/BalloonParty/Game Render Maps")]
        private static void Open()
        {
            GetWindow<GameRenderMapsWindow>("Game Render Maps");
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnDestroy()
        {
            if (_channelMaterial != null)
            {
                DestroyImmediate(_channelMaterial);
            }
        }

        private void OnGUI()
        {
            DrawMapSelector();
            EditorGUILayout.Space();

            var descriptor = Descriptors[_selectedIndex];

            if (descriptor.IsCustom)
            {
                _customTexture = (Texture)EditorGUILayout.ObjectField(
                    "Texture", _customTexture, typeof(Texture), false);
            }

            DrawChannelToggles(descriptor);

            if (descriptor.HasPaletteChannel)
            {
                _decodePalette = EditorGUILayout.ToggleLeft(
                    new GUIContent("Decode A to palette color",
                        "With only A selected, show the color each encoded index maps to (black = untagged) " +
                        "instead of the raw grayscale code."),
                    _decodePalette);
            }

            EditorGUILayout.Space();

            var texture = ResolveTexture(descriptor);

            if (texture == null)
            {
                EditorGUILayout.HelpBox(UnavailableMessage(descriptor), MessageType.Info);
                return;
            }

            DrawPreview(texture, descriptor.HasPaletteChannel && _decodePalette);
            DrawFooter(texture);
        }

        private void DrawMapSelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Map", GUILayout.Width(32));
            _selectedIndex = EditorGUILayout.Popup(_selectedIndex, MapNames, EditorStyles.toolbarPopup);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChannelToggles(MapDescriptor descriptor)
        {
            EditorGUILayout.BeginHorizontal();

            for (var c = 0; c < _channelEnabled.Length; c++)
            {
                var content = new GUIContent(ChannelLabels[c], ChannelTooltip(descriptor, c));
                _channelEnabled[c] = GUILayout.Toggle(_channelEnabled[c], content, EditorStyles.toolbarButton);
            }

            EditorGUILayout.EndHorizontal();
        }

        private Texture ResolveTexture(MapDescriptor descriptor)
        {
            return descriptor.IsCustom ? _customTexture : descriptor.Fetch();
        }

        private static string UnavailableMessage(MapDescriptor descriptor)
        {
            if (descriptor.IsCustom)
            {
                return "Assign a texture above to preview it.";
            }

            return !Application.isPlaying
                ? $"Enter Play Mode — {descriptor.Name} only binds during gameplay."
                : descriptor.UnavailableHint;
        }

        private void DrawPreview(Texture texture, bool decodePalette)
        {
            var material = EnsureMaterial();
            var aspect = (float)texture.width / texture.height;
            var rect = GUILayoutUtility.GetAspectRect(aspect);

            if (material != null)
            {
                material.SetVector(ChannelMaskId, MaskVector());
                material.SetFloat(DecodePaletteId, decodePalette ? 1f : 0f);
                if (decodePalette)
                {
                    PushPalette(material);
                }

                EditorGUI.DrawPreviewTexture(rect, texture, material);
            }
            else
            {
                // ChannelPreview.shader can't be validated by dotnet build (it doesn't compile
                // shaders) — a typo there shows as a magenta/blank preview. Fall back to the raw
                // texture instead of breaking the window if the shader failed to load.
                EditorGUI.DrawPreviewTexture(rect, texture);
            }
        }

        // Same index order the stampers encode (IGamePalette.Colors); missing asset = all black.
        private void PushPalette(Material material)
        {
            Array.Clear(_paletteBuffer, 0, _paletteBuffer.Length);

            var palette = _paletteCache.Value;
            if (palette != null)
            {
                IGamePalette source = palette;
                var count = Mathf.Min(source.Colors.Count, _paletteBuffer.Length);
                for (var i = 0; i < count; i++)
                {
                    _paletteBuffer[i] = source.Colors[i].Color;
                }
            }

            material.SetVectorArray(PaletteColorsId, _paletteBuffer);
        }

        private Vector4 MaskVector()
        {
            return new Vector4(
                _channelEnabled[0] ? 1f : 0f,
                _channelEnabled[1] ? 1f : 0f,
                _channelEnabled[2] ? 1f : 0f,
                _channelEnabled[3] ? 1f : 0f);
        }

        private Material EnsureMaterial()
        {
            if (_channelMaterial != null)
            {
                return _channelMaterial;
            }

            // Shader.Find (rather than a serialized reference) is fine here — this is an
            // editor-only tool window, never shipped, so build-stripping of Shader.Find-only
            // references (the reason ScreenSpaceLightService serializes its shaders) doesn't apply.
            var shader = Shader.Find("Hidden/BalloonParty/Editor/ChannelPreview");
            if (shader == null)
            {
                return null;
            }

            _channelMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _channelMaterial;
        }

        private static void DrawFooter(Texture texture)
        {
            EditorGUILayout.LabelField($"{texture.width} × {texture.height}   {texture.graphicsFormat}");
        }

        private static string ChannelTooltip(MapDescriptor descriptor, int channelIndex)
        {
            if (descriptor.IsCustom)
            {
                return string.Format(GenericChannelTooltipFormat, ChannelLabels[channelIndex]);
            }

            return channelIndex switch
            {
                0 => descriptor.ChannelR,
                1 => descriptor.ChannelG,
                2 => descriptor.ChannelB,
                _ => descriptor.ChannelA,
            };
        }

        private static Texture FetchLightBuffer()
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            // Ping-ponged (see ScreenSpaceLightService.LightTexture) — re-resolved every call
            // rather than cached, so a repaint always sees whichever buffer is current.
            var service = UnityEngine.Object.FindFirstObjectByType<ScreenSpaceLightService>();
            return service != null ? service.LightTexture : null;
        }

        private static MapDescriptor[] BuildDescriptors()
        {
            return new[]
            {
                new MapDescriptor(
                    "Scene Capture",
                    () => Application.isPlaying ? Shader.GetGlobalTexture(SceneCaptureTexId) : null,
                    "No scene capture bound yet — it renders once a consumer acquires it (e.g. spawn an Unbreakable).",
                    "Downscaled capture-layer scene color — red channel.",
                    "Downscaled capture-layer scene color — green channel.",
                    "Downscaled capture-layer scene color — blue channel.",
                    "Sprite coverage mask — ~0 over open sky/ground, ~1 over casters. The capture " +
                    "camera clears with alpha 0 (SceneCaptureService.ApplyBackgroundColor), so this " +
                    "doubles as the GI light buffer's occlusion/shadow source."),

                new MapDescriptor(
                    "Disturbance Field",
                    () => Application.isPlaying ? Shader.GetGlobalTexture(DisturbanceTexId) : null,
                    "No disturbance field bound — the field binds once the game scope starts.",
                    "Density — 1.0 = equilibrium (undisturbed); stamps dig it toward 0.0 as cloud/foliage displaces.",
                    "Displacement X — 0.5-biased (0.5 = zero offset), pushed away from the stamp direction.",
                    "Displacement Y — 0.5-biased (0.5 = zero offset), pushed away from the stamp direction.",
                    "Palette tag, packed (index + life) / 16 — 16 slots, in-slot value = remaining life, " +
                    "drained each diffusion tick (ColorTagDecay); 0 = untagged. Written hard (never " +
                    "blended); agitated specks adopt the slot and flush toward that color.",
                    hasPaletteChannel: true),

                new MapDescriptor(
                    "Disturbance Colour",
                    () => Application.isPlaying ? Shader.GetGlobalTexture(DisturbanceColorTexId) : null,
                    "No colour layer bound — assign the Color Lerp Shader on DisturbanceFieldSettings; it binds once the game scope starts.",
                    "Smoothed palette colour — red. The tag index is decoded to its palette colour, then eased.",
                    "Smoothed palette colour — green.",
                    "Smoothed palette colour — blue.",
                    "Strength — the tag's eased life (0 = untinted). Overwrites crossfade here instead of snapping, " +
                    "and it's the smooth, band-free colour consumers sample."),

                new MapDescriptor(
                    "GI Light Buffer",
                    FetchLightBuffer,
                    "No light buffer yet — ScreenSpaceLightService isn't in the scene, or it hasn't built its first frame yet.",
                    "Bounce color, red — scene color marched toward the light, minus ambient sky.",
                    "Bounce color, green — scene color marched toward the light, minus ambient sky.",
                    "Bounce color, blue — scene color marched toward the light, minus ambient sky.",
                    "Shadow amount — occluder coverage marched away from the light, masked off the " +
                    "casters themselves so only the ground beside them darkens."),

                new MapDescriptor("Custom…", null, null, null, null, null, null),
            };
        }

        private sealed class MapDescriptor
        {
            public readonly string Name;
            public readonly Func<Texture> Fetch;
            public readonly string UnavailableHint;
            public readonly string ChannelR;
            public readonly string ChannelG;
            public readonly string ChannelB;
            public readonly string ChannelA;
            public readonly bool HasPaletteChannel;

            public bool IsCustom => Fetch == null;

            public MapDescriptor(string name, Func<Texture> fetch, string unavailableHint,
                string channelR, string channelG, string channelB, string channelA,
                bool hasPaletteChannel = false)
            {
                Name = name;
                Fetch = fetch;
                UnavailableHint = unavailableHint;
                ChannelR = channelR;
                ChannelG = channelG;
                ChannelB = channelB;
                ChannelA = channelA;
                HasPaletteChannel = hasPaletteChannel;
            }
        }
    }
}

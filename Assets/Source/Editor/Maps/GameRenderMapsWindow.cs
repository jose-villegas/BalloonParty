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
        private static readonly int SceneLightTexId = Shader.PropertyToID("_SceneLightTex");
        private static readonly int CloudDensityTexId = Shader.PropertyToID("_CloudDensityTex");
        private static readonly int ChannelMaskId = Shader.PropertyToID("_ChannelMask");
        private static readonly int PaletteColorsId = Shader.PropertyToID("_PaletteColors");
        private static readonly int DecodePaletteId = Shader.PropertyToID("_DecodePalette");
        private static readonly int MipLevelId = Shader.PropertyToID("_MipLevel");
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
        private float _mipLevel;
        private float _zoom = 1f;
        private Vector2 _scrollPos;

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

            if (descriptor.HasMipChain)
            {
                _mipLevel = EditorGUILayout.Slider(
                    new GUIContent("Mip Level",
                        "Preview a specific mip level of the texture — shows what the cone march " +
                        "sees at each tap distance (0 = full res, higher = averaged over wider area)."),
                    _mipLevel, 0f, descriptor.MaxMipLevel);
            }

            EditorGUILayout.Space();

            var texture = ResolveTexture(descriptor);

            if (texture == null)
            {
                EditorGUILayout.HelpBox(UnavailableMessage(descriptor), MessageType.Info);
                return;
            }

            DrawInfoBar(texture);
            DrawPreview(texture, descriptor.HasPaletteChannel && _decodePalette);
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

        private void DrawInfoBar(Texture texture)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                $"{texture.width} × {texture.height}   {texture.graphicsFormat}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (_zoom > 1.001f)
            {
                GUILayout.Label($"{_zoom:F1}×", EditorStyles.miniLabel);
                if (GUILayout.Button("1:1", EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    _zoom = 1f;
                    _scrollPos = Vector2.zero;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(Texture texture, bool decodePalette)
        {
            var material = EnsureMaterial();
            float texAspect = (float)texture.width / texture.height;

            // Reserve all remaining window space for the preview.
            var availableRect = GUILayoutUtility.GetRect(
                0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (availableRect.width < 1f || availableRect.height < 1f)
            {
                return;
            }

            // Largest aspect-correct rect that fits the available area at zoom = 1.
            float rectAspect = availableRect.width / availableRect.height;
            float fittedW, fittedH;
            if (texAspect > rectAspect)
            {
                fittedW = availableRect.width;
                fittedH = availableRect.width / texAspect;
            }
            else
            {
                fittedH = availableRect.height;
                fittedW = availableRect.height * texAspect;
            }

            float contentW = fittedW * _zoom;
            float contentH = fittedH * _zoom;

            HandleZoom(availableRect, fittedW, fittedH, ref contentW, ref contentH);
            HandlePan(availableRect);

            _scrollPos.x = Mathf.Clamp(_scrollPos.x, 0f, Mathf.Max(0f, contentW - availableRect.width));
            _scrollPos.y = Mathf.Clamp(_scrollPos.y, 0f, Mathf.Max(0f, contentH - availableRect.height));

            _scrollPos = GUI.BeginScrollView(
                availableRect, _scrollPos, new Rect(0, 0, contentW, contentH));

            var drawRect = new Rect(0, 0, contentW, contentH);

            if (material != null)
            {
                material.SetVector(ChannelMaskId, MaskVector());
                material.SetFloat(DecodePaletteId, decodePalette ? 1f : 0f);
                material.SetFloat(MipLevelId, _mipLevel);
                if (decodePalette)
                {
                    PushPalette(material);
                }

                EditorGUI.DrawPreviewTexture(drawRect, texture, material);
            }
            else
            {
                EditorGUI.DrawPreviewTexture(drawRect, texture);
            }

            GUI.EndScrollView();
        }

        private void HandleZoom(Rect availableRect, float fittedW, float fittedH,
            ref float contentW, ref float contentH)
        {
            var e = Event.current;
            if (e.type != EventType.ScrollWheel || !availableRect.Contains(e.mousePosition))
            {
                return;
            }

            var mouseLocal = e.mousePosition - availableRect.position;
            var mouseInContent = mouseLocal + _scrollPos;
            var mouseNorm = new Vector2(
                contentW > 0f ? mouseInContent.x / contentW : 0.5f,
                contentH > 0f ? mouseInContent.y / contentH : 0.5f);

            float zoomFactor = e.delta.y > 0 ? 1f / 1.15f : 1.15f;
            _zoom = Mathf.Clamp(_zoom * zoomFactor, 1f, 20f);

            contentW = fittedW * _zoom;
            contentH = fittedH * _zoom;

            // Keep the texel under the pointer stationary.
            _scrollPos = new Vector2(
                mouseNorm.x * contentW - mouseLocal.x,
                mouseNorm.y * contentH - mouseLocal.y);

            e.Use();
        }

        private void HandlePan(Rect availableRect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDrag || e.button != 2 ||
                !availableRect.Contains(e.mousePosition))
            {
                return;
            }

            _scrollPos -= e.delta;
            e.Use();
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
                    "doubles as the GI light buffer's occlusion/shadow source.",
                    hasMipChain: true),

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
                    "Scene Light Field",
                    () => Application.isPlaying ? Shader.GetGlobalTexture(SceneLightTexId) : null,
                    "No light field bound — SceneLightFieldService binds it once the game scope starts.",
                    "Magnitude — light amount. 1.0 = the key light's intensity at rest; sources add " +
                    "above it, soft-capped so overlaps never blow out.",
                    "Direction X — 0.5-biased toward-light direction (0.5 = zero). Recomputed each tick " +
                    "from grad(R), so it points toward the brightest nearby source.",
                    "Direction Y — 0.5-biased toward-light direction (see G).",
                    "Palette tag, (index + 1) / 16 — 16 slots; 0 = untagged (consumers use the global " +
                    "key-light colour). Written hard (dominant source wins), never blended.",
                    hasPaletteChannel: true),

                new MapDescriptor(
                    "GI Light Buffer",
                    FetchLightBuffer,
                    "No light buffer yet — ScreenSpaceLightService isn't in the scene, or it hasn't built its first frame yet.",
                    "Bounce color, red — scene color marched toward the light, minus ambient sky.",
                    "Bounce color, green — scene color marched toward the light, minus ambient sky.",
                    "Bounce color, blue — scene color marched toward the light, minus ambient sky.",
                    "Shadow amount — occluder coverage marched away from the light, masked off the " +
                    "casters themselves so only the ground beside them darkens."),

                new MapDescriptor(
                    "Cloud Field",
                    () => Application.isPlaying ? Shader.GetGlobalTexture(CloudDensityTexId) : null,
                    "No cloud field bound — CloudFieldService binds it once the game scope starts.",
                    "Cloud density — thresholded [0,1] cloud intensity, baked from the scrolling three-octave " +
                    "noise. Every consumer (BackgroundCloud backdrop, sprite drop-shadows, the GI light smear) " +
                    "taps this same map.",
                    "Unused — single-channel (R8) density map.",
                    "Unused — single-channel (R8) density map.",
                    "Unused — single-channel (R8) density map."),

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
            public readonly bool HasMipChain;

            public bool IsCustom => Fetch == null;

            public float MaxMipLevel
            {
                get
                {
                    var tex = Fetch?.Invoke();
                    if (tex == null)
                    {
                        return 4f;
                    }

                    return Mathf.Log(Mathf.Max(tex.width, tex.height), 2f);
                }
            }

            public MapDescriptor(string name, Func<Texture> fetch, string unavailableHint,
                string channelR, string channelG, string channelB, string channelA,
                bool hasPaletteChannel = false, bool hasMipChain = false)
            {
                Name = name;
                Fetch = fetch;
                UnavailableHint = unavailableHint;
                ChannelR = channelR;
                ChannelG = channelG;
                ChannelB = channelB;
                ChannelA = channelA;
                HasPaletteChannel = hasPaletteChannel;
                HasMipChain = hasMipChain;
            }
        }
    }
}

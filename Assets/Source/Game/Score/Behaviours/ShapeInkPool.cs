using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Rendering;
using BalloonParty.UI.Score;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Hands <see cref="ShapeFormationTicker" /> a <see cref="ShapeInkView" /> for each shape in flight and
    ///     takes it back when the shape is done. Owns what the views share: each shape's baked mesh, built the
    ///     first time that shape flies, and three copies of the ink material carrying the ink ramp, drawn in
    ///     turn: the shape's drop shadow, the half of it behind its centre, and the half in front.
    /// </summary>
    internal sealed class ShapeInkPool : IDisposable
    {
        // Room past the ink's own edge for the mesh ribbon's antialiased rim, in world units.
        private const float RimPadding = 0.01f;

        private static readonly int InkRampId = Shader.PropertyToID("_InkRamp");
        private static readonly int InkHalfId = Shader.PropertyToID("_InkHalf");
        private static readonly int InkShadowId = Shader.PropertyToID("_InkShadow");
        private static readonly int ShadowStencilCompId = Shader.PropertyToID("_ShadowStencilComp");
        private static readonly int ShadowStencilPassId = Shader.PropertyToID("_ShadowStencilPass");
        private static readonly int PenHandId = Shader.PropertyToID("_PenHand");
        private static readonly int SketchId = Shader.PropertyToID("_Sketch");

        private readonly Dictionary<FormationShape, Mesh> _meshes = new();
        private readonly Stack<ShapeInkView> _free = new();
        private readonly List<ShapeInkView> _views = new();
        private readonly Color32[] _pixels = new Color32[ShapeInkRamp.Size];

        // The shadow's material, the far half's, then the near half's: one for each of the mesh's submeshes.
        private readonly Material[] _materials = new Material[ShapeInkMeshBuilder.Layers];

        private Material _source;
        private Texture2D _ramp;
        private Vector3 _size;
        private bool _warned;

        public void Dispose()
        {
            foreach (var view in _views)
            {
                if (view != null)
                {
                    UnityEngine.Object.Destroy(view.gameObject);
                }
            }

            foreach (var mesh in _meshes.Values)
            {
                UnityEngine.Object.Destroy(mesh);
            }

            _views.Clear();
            _free.Clear();
            _meshes.Clear();
            DestroyMaterial();
        }

        /// <summary>
        ///     A view showing <paramref name="shape" /> in <paramref name="color" />, sorted and layered as
        ///     <paramref name="guide" />. Null when the settings have no ink to draw with
        ///     (<see cref="ShapeInkSettings.Drawable" />): the shape then flies undrawn, its points still counted.
        /// </summary>
        internal ShapeInkView Acquire(FormationShape shape, in ShapeInkSettings settings, Color color, FlyingTrail guide)
        {
            if (!settings.Drawable)
            {
                if (!_warned)
                {
                    _warned = true;
                    Log.Warn("ShapeInk", "The score shapes have no ink material or curves set, so they fly undrawn.");
                }

                return null;
            }

            PrepareMaterial(in settings);

            if (!_meshes.TryGetValue(shape, out var mesh))
            {
                mesh = ShapeInkMeshBuilder.Build(shape);
                _meshes.Add(shape, mesh);
            }

            var view = _free.Count > 0 ? _free.Pop() : Create(guide.gameObject.layer);
            view.Show(mesh, _materials, color, _size, guide);
            return view;
        }

        internal void Release(ShapeInkView view)
        {
            if (view == null)
            {
                return;
            }

            view.Hide();
            _free.Push(view);
        }

        private ShapeInkView Create(int layer)
        {
            var root = new GameObject("ShapeInk") { layer = layer };
            var view = root.AddComponent<ShapeInkView>();

            // An effects map renders its layer only while something tracked is alive on it.
            LayerPresence.Track(root);
            _views.Add(view);
            return view;
        }

        private void PrepareMaterial(in ShapeInkSettings settings)
        {
            var fresh = _materials[0] == null || _source != settings.Material;
            if (fresh)
            {
                DestroyMaterial();
                _source = settings.Material;
                _ramp = new Texture2D(ShapeInkRamp.Size, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "ShapeInkRamp",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                for (var layer = 0; layer < _materials.Length; layer++)
                {
                    var material = new Material(_source) { hideFlags = HideFlags.HideAndDontSave };
                    material.SetTexture(InkRampId, _ramp);

                    // The shadow is the whole shape; the ink is its far half, then its near half.
                    var shadow = layer == 0;
                    material.SetFloat(InkShadowId, shadow ? 1f : 0f);
                    material.SetFloat(InkHalfId, shadow ? 0f : layer == 1 ? -1f : 1f);

                    // A pixel takes the shadow once: the shadow marks it in the stencil and skips marked ones.
                    material.SetFloat(ShadowStencilCompId, (float)(shadow ? CompareFunction.NotEqual : CompareFunction.Always));
                    material.SetFloat(ShadowStencilPassId, (float)(shadow ? StencilOp.Replace : StencilOp.Keep));

                    // Same sorting layer and order, so the queue decides the order the three draw in.
                    material.renderQueue = _source.renderQueue + layer;
                    _materials[layer] = material;
                }
            }

#if UNITY_EDITOR
            // The curves can be retuned in the inspector mid-play; a build's never change.
            fresh = true;
#endif

            if (!fresh)
            {
                return;
            }

            var widest = ShapeInkRamp.Fill(in settings, _pixels);
            _ramp.SetPixels32(_pixels);
            _ramp.Apply(false, false);

            var inkHalfWidth = widest * 0.5f;
            _size = new Vector3(
                inkHalfWidth + RimPadding + StrokeRoom(_materials[0]), inkHalfWidth, settings.Width.Evaluate(0f) * 0.5f);
        }

        // The ink is drawn off the walk's line: the stroke wavers, and a second one rides beside it. The mesh
        // ribbon needs that much more room each side.
        private static float StrokeRoom(Material material)
        {
            return Mathf.Abs(material.GetVector(PenHandId).x) + Mathf.Abs(material.GetVector(SketchId).y);
        }

        private void DestroyMaterial()
        {
            for (var layer = 0; layer < _materials.Length; layer++)
            {
                if (_materials[layer] != null)
                {
                    UnityEngine.Object.Destroy(_materials[layer]);
                }

                _materials[layer] = null;
            }

            if (_ramp != null)
            {
                UnityEngine.Object.Destroy(_ramp);
            }

            _ramp = null;
            _source = null;
        }
    }
}

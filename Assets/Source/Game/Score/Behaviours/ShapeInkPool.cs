using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Rendering;
using BalloonParty.UI.Score;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Hands <see cref="ShapeFormationTicker" /> a <see cref="ShapeInkView" /> for each shape in flight and
    ///     takes it back when the shape is done. Owns what the views share: each shape's baked mesh, built the
    ///     first time that shape flies, and two copies of the ink material carrying the ink ramp, one drawing
    ///     the half of a shape behind its centre and one, after it, the half in front.
    /// </summary>
    internal sealed class ShapeInkPool : IDisposable
    {
        // Room past the ink's own edge for the mesh ribbon's antialiased rim, in world units.
        private const float RimPadding = 0.01f;

        private static readonly int InkRampId = Shader.PropertyToID("_InkRamp");
        private static readonly int InkHalfId = Shader.PropertyToID("_InkHalf");
        private static readonly int InkLookId = Shader.PropertyToID("_InkLook");
        private static readonly int PenHandId = Shader.PropertyToID("_PenHand");
        private static readonly int SketchId = Shader.PropertyToID("_Sketch");

        private readonly Dictionary<FormationShape, Mesh> _meshes = new();
        private readonly Stack<ShapeInkView> _free = new();
        private readonly List<ShapeInkView> _views = new();
        private readonly Color32[] _pixels = new Color32[ShapeInkRamp.Size];

        // The far half's material, then the near half's: one for each of the mesh's two submeshes.
        private readonly Material[] _materials = new Material[2];

        private Material _source;
        private Texture2D _ramp;
        private Vector3 _size;

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

        /// <summary>A view showing <paramref name="shape" /> in <paramref name="color" />, sorted and layered as <paramref name="guide" />.</summary>
        internal ShapeInkView Acquire(FormationShape shape, in ShapeInkSettings settings, Color color, FlyingTrail guide)
        {
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

                for (var half = 0; half < _materials.Length; half++)
                {
                    var material = new Material(_source) { hideFlags = HideFlags.HideAndDontSave };
                    material.SetTexture(InkRampId, _ramp);
                    material.SetFloat(InkHalfId, half == 0 ? -1f : 1f);

                    // Same sorting layer and order, so the queue decides: the near half after the far half.
                    material.renderQueue = _source.renderQueue + half;
                    _materials[half] = material;
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

        // The pen-on-paper look draws off the walk's line: the stroke wavers, and a second one rides beside
        // it. The mesh ribbon needs that much more room each side.
        private static float StrokeRoom(Material material)
        {
            if (material.GetFloat(InkLookId) < 0.5f)
            {
                return 0f;
            }

            return Mathf.Abs(material.GetVector(PenHandId).x) + Mathf.Abs(material.GetVector(SketchId).y);
        }

        private void DestroyMaterial()
        {
            for (var half = 0; half < _materials.Length; half++)
            {
                if (_materials[half] != null)
                {
                    UnityEngine.Object.Destroy(_materials[half]);
                }

                _materials[half] = null;
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

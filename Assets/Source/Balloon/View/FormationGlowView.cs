using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The blue light ring round unbreakables in formation, on the ground beneath them (<c>FormationGlow</c>
    ///     shader): rings that join like metaballs as members draw close, lit only as close as they are. One quad,
    ///     fitted each frame round the members that glow, so the shader only runs where the light can be; hidden,
    ///     and costing nothing, while no member glows.
    /// </summary>
    internal sealed class FormationGlowView : MonoBehaviour
    {
        private static readonly int PointsId = Shader.PropertyToID("_GlowPoints");
        private static readonly int CountId = Shader.PropertyToID("_GlowCount");
        private static readonly int FieldId = Shader.PropertyToID("_GlowField");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HdrBoostId = Shader.PropertyToID("_HdrBoost");

        private readonly Vector4[] _points = new Vector4[FormationGlowField.MaxPoints];

        private IFormationGlowSettings _settings;
        private FormationGlowSources _sources;
        private Material _material;
        private MeshRenderer _renderer;
        private Transform _quad;

        // After the balloons have moved this frame, so the light keeps to them as they glide.
        private void LateUpdate()
        {
            if (_renderer == null)
            {
                return;
            }

            var bounds = default(Rect);
            var count = _settings.Enabled ? FormationGlowField.Fill(_sources.All, _settings, _points, out bounds) : 0;
            if (count == 0)
            {
                _renderer.enabled = false;
                return;
            }

            _quad.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            _quad.localScale = new Vector3(bounds.width, bounds.height, 1f);
            _material.SetVectorArray(PointsId, _points);
            _material.SetFloat(CountId, count);
            _renderer.enabled = true;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        [Inject]
        internal void Construct(IBalloonsConfiguration balloons, FormationGlowSources sources, IGamePalette palette)
        {
            _settings = balloons.FormationGlow;
            _sources = sources;

            var shader = _settings.Shader;
            if (shader == null)
            {
                Log.Warn("FormationGlow", "no shader on BalloonsConfiguration's formation glow: no ring.", this);
                return;
            }

            _material = new Material(shader) { name = "FormationGlow" };
            _material.SetColor(ColorId, palette.GetColor(GamePalette.UnbreakableFormationColorId));
            _material.SetFloat(HdrBoostId, _settings.HdrBoost);
            var reach = _settings.Reach;
            _material.SetVector(FieldId, new Vector4(
                reach * reach, FormationGlowField.Threshold(_settings.RingRadius, reach), _settings.RingWidth,
                _settings.Fill));

            // Default layer: the main camera draws it in play, but neither the launch screen nor the GI capture
            // (as an occluder, a ring would cast a shadow on the ground).
            var quad = QuadRendererBuilder.Build(
                "FormationGlowQuad", _material, 0, _settings.SortingLayerName, _settings.SortingOrder);
            _quad = quad.transform;
            _quad.SetParent(transform, false);
            _renderer = quad.GetComponent<MeshRenderer>();
            _renderer.enabled = false;
        }
    }
}

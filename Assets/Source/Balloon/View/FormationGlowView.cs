using System;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The blue light ring round unbreakables in formation, on the ground beneath them (<c>FormationGlow</c>
    ///     shader): rings that join like metaballs as members draw close, lit only as close as they are. One quad,
    ///     fitted each frame round the members that glow, so the shader only runs where the light can be; hidden,
    ///     and costing nothing, while no member glows. The rings also light what stands round them: the quad is
    ///     only drawn, so each group of joined rings casts one capsule light through the scene-light field
    ///     (<see cref="FormationGlowBeams" />).
    /// </summary>
    internal sealed class FormationGlowView : MonoBehaviour
    {
        private static readonly int PointsId = Shader.PropertyToID("_GlowPoints");
        private static readonly int CountId = Shader.PropertyToID("_GlowCount");
        private static readonly int FieldId = Shader.PropertyToID("_GlowField");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HdrBoostId = Shader.PropertyToID("_HdrBoost");

        private static readonly int DissolveId = Shader.PropertyToID("_GlowDissolve");

        private static int BalloonsLayer = -1;

        private readonly Vector4[] _points = new Vector4[FormationGlowField.MaxPoints];
        private readonly FormationGlowField _field = new();
        private readonly int[] _groups = new int[FormationGlowField.MaxPoints];
        private readonly FormationGlowBeam[] _beams = new FormationGlowBeam[FormationGlowBeams.MaxBeams];
        private readonly Light[] _lights = new Light[FormationGlowBeams.MaxBeams];
        private readonly IDisposable[] _lit = new IDisposable[FormationGlowBeams.MaxBeams];

        private IFormationGlowSettings _settings;
        private FormationGlowSources _sources;
        private SceneLightFieldService _lightField;
        private int _lightColorIndex;
        private float _lightJoinDistance;
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
            var count = _settings.Enabled ? _field.Step(_sources.All, _settings, Time.deltaTime, _points, out bounds) : 0;
            CastLight(count);
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
            CastLight(0);
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        [Inject]
        internal void Construct(
            IBalloonsConfiguration balloons, FormationGlowSources sources, IGamePalette palette,
            SceneLightFieldService lightField)
        {
            _settings = balloons.FormationGlow;
            _sources = sources;
            _lightField = lightField;
            _lightColorIndex = palette.PaletteIndexOf(GamePalette.UnbreakableFormationColorId);
            _lightJoinDistance = FormationGlowField.JoinDistance(_settings.RingRadius, _settings.Reach);

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
            _material.SetVector(DissolveId, new Vector4(_settings.DissolveNoiseScale, 0f, 0f, 0f));

            if (BalloonsLayer == -1)
            {
                BalloonsLayer = LayerMask.NameToLayer("Balloons");
            }

            // The balloons' layer: the main camera draws it in play, the launch screen doesn't, and the GI capture
            // does, so the rings' light is in the colour the GI smear bounces. The shader writes colour only, so
            // a ring adds no coverage there and casts no shadow.
            var quad = QuadRendererBuilder.Build(
                "FormationGlowQuad", _material, BalloonsLayer, _settings.SortingLayerName, _settings.SortingOrder);
            _quad = quad.transform;
            _quad.SetParent(transform, false);
            _renderer = quad.GetComponent<MeshRenderer>();
            _renderer.enabled = false;
        }

        // A light is on only while its group glows: the field loops over every registered light, a dark one too.
        // A still formation writes the same values each frame, which the field doesn't re-render for.
        private void CastLight(int count)
        {
            var beams = _lightField != null && _settings.LightIntensity > 0f
                ? FormationGlowBeams.Group(_points, count, _lightJoinDistance, _groups, _beams)
                : 0;
            for (var i = 0; i < _lights.Length; i++)
            {
                if (i >= beams)
                {
                    _lit[i]?.Dispose();
                    _lit[i] = null;
                    continue;
                }

                var beam = _beams[i];
                var light = _lights[i] ??= new Light(
                    beam.Start, _settings.LightRadius, 0f, _lightColorIndex, _settings.LightFalloffPower);
                light.Position.Value = beam.Start;
                light.EndPosition.Value = beam.End;
                light.Intensity.Value = _settings.LightIntensity * beam.Level;
                _lit[i] ??= _lightField.RegisterLight(light);
            }
        }
    }
}

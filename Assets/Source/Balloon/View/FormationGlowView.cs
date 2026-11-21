using System;
using BalloonParty.Balloon.Formation;
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
    ///     (<see cref="FormationGlowBeams" />). The balloon an item cover stands round gets a ring of that
    ///     formation too (<see cref="FormationGlowHosts" />), so the outline closes over it.
    /// </summary>
    internal sealed class FormationGlowView : MonoBehaviour
    {
        private static readonly int PointsId = Shader.PropertyToID("_GlowPoints");
        private static readonly int CountId = Shader.PropertyToID("_GlowCount");
        private static readonly int FormationsId = Shader.PropertyToID("_GlowFormations");
        private static readonly int FieldId = Shader.PropertyToID("_GlowField");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HdrBoostId = Shader.PropertyToID("_HdrBoost");

        private static readonly int DissolveId = Shader.PropertyToID("_GlowDissolve");

        private static int EffectsLayer = -1;

        private readonly Vector4[] _points = new Vector4[FormationGlowField.MaxPoints];
        private readonly float[] _formations = new float[FormationGlowField.MaxPoints];
        private readonly FormationGlowField _field = new();
        private readonly FormationGlowHosts _hosts = new();
        private readonly int[] _groups = new int[FormationGlowField.MaxPoints];
        private readonly FormationGlowBeam[] _beams = new FormationGlowBeam[FormationGlowBeams.MaxBeams];
        private readonly Light[] _lights = new Light[FormationGlowBeams.MaxBeams];
        private readonly IDisposable[] _lit = new IDisposable[FormationGlowBeams.MaxBeams];

        private IFormationGlowSettings _settings;
        private FormationGlowSources _sources;
        private ICoveredHosts _coveredHosts;
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
            var count = 0;
            if (_settings.Enabled)
            {
                _hosts.Sync(_coveredHosts.CoveredHosts, _sources);
                count = _field.Step(_sources.All, _settings, Time.deltaTime, _points, _formations, out bounds);
            }

            CastLight(count);
            if (count == 0)
            {
                _renderer.enabled = false;
                return;
            }

            _quad.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            _quad.localScale = new Vector3(bounds.width, bounds.height, 1f);
            _material.SetVectorArray(PointsId, _points);
            _material.SetFloatArray(FormationsId, _formations);
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
            SceneLightFieldService lightField, ICoveredHosts coveredHosts)
        {
            _settings = balloons.FormationGlow;
            _sources = sources;
            _coveredHosts = coveredHosts;
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

            if (EffectsLayer == -1)
            {
                EffectsLayer = LayerMask.NameToLayer("Effects");
            }

            // The effects layer: a flying ball's lens bends the rings around it. While one flies the rings are
            // drawn with the effects, over the board, and not under the balloons where they sort: the owner
            // chose the lens over the order (2026-11-20). The GI capture draws the layer too, so the rings'
            // light is in the colour the GI smear bounces. The shader writes colour only, so a ring adds no
            // coverage there and casts no shadow.
            var quad = QuadRendererBuilder.Build(
                "FormationGlowQuad", _material, EffectsLayer, _settings.SortingLayerName, _settings.SortingOrder);
            _quad = quad.transform;
            _quad.SetParent(transform, false);
            _renderer = quad.GetComponent<MeshRenderer>();
            _renderer.enabled = false;

            // Not pooled: the effects map knows a ring is up by its renderer, which is on only while one glows.
            LayerPresence.Watch(_renderer);
        }

        // A light is on only while its group glows: the field loops over every registered light, a dark one too.
        // A still formation writes the same values each frame, which the field doesn't re-render for.
        private void CastLight(int count)
        {
            var beams = _lightField != null && _settings.LightIntensity > 0f
                ? FormationGlowBeams.Group(_points, _formations, count, _lightJoinDistance, _groups, _beams)
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

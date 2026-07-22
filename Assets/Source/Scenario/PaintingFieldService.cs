using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     Owns the painting field: a persistent screen-space RT that accumulates palette-colored stamps
    ///     and decays them over time. Publishes the RT as global shader properties
    ///     (<c>_PaintingTex</c>, <c>_PaintingBoundsMin/Size</c>) so any consumer can sample it.
    ///     Architecture mirrors <c>DisturbanceFieldService</c> / <c>SceneLightFieldService</c>:
    ///     plain-C# DI singleton, no MonoBehaviour, ping-pong blit pipeline.
    /// </summary>
    internal sealed class PaintingFieldService : IStartable, ITickable, IDisposable
    {
        private const int MaxStampsPerBatch = 32;

        private static readonly int BoundsMinId = Shader.PropertyToID("_PaintingBoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_PaintingBoundsSize");
        private static readonly int ActiveId = Shader.PropertyToID("_PaintingFieldActive");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int StampCentersId = Shader.PropertyToID("_StampCenters");
        private static readonly int StampRadiiId = Shader.PropertyToID("_StampRadii");
        private static readonly int StampColorsId = Shader.PropertyToID("_StampColors");
        private static readonly int DecayRateId = Shader.PropertyToID("_DecayRate");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimePhaseId = Shader.PropertyToID("_TimePhase");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindDirId = Shader.PropertyToID("_WindDir");
        private static readonly int WindAgeBiasId = Shader.PropertyToID("_WindAgeBias");
        private static readonly int PaintingTimeId = Shader.PropertyToID("_PaintingTime");

        private readonly IPaintingFieldSettings _settings;
        private readonly IGameDisplayConfiguration _display;
        private readonly IGamePalette _palette;
        private readonly PaintingFieldResources _resources = new();
        private readonly List<PendingStamp> _pendingStamps = new();
        private readonly Vector4[] _batchCenters = new Vector4[MaxStampsPerBatch];
        private readonly float[] _batchRadii = new float[MaxStampsPerBatch];
        private readonly Vector4[] _batchColors = new Vector4[MaxStampsPerBatch];

        private DisturbanceFieldCoordinates _coords;
        private Rect _bounds;
        private float _lastDecayTime;
        private float _timePhase;
        private float _paintingTime;
        private float _windDampen = 1f;

        public PaintingFieldService(
            IPaintingFieldSettings settings,
            IGameDisplayConfiguration display,
            IGamePalette palette)
        {
            _settings = settings;
            _display = display;
            _palette = palette;
        }

        void IStartable.Start()
        {
            if (_settings?.StampShader == null || _settings.DecayShader == null || _display == null)
            {
                Log.Warn("PaintingField", "disabled: assign stamp + decay shaders on PaintingFieldSettings.");
                return;
            }

            _coords = new DisturbanceFieldCoordinates(_display, _settings.TexelsPerUnit);
            _bounds = _coords.Bounds;
            _resources.Initialize(_settings, _coords.Width, _coords.Height);

            PushBoundsGlobals();
            Shader.SetGlobalFloat(ActiveId, 1f);
            _lastDecayTime = Time.time;
        }

        void ITickable.Tick()
        {
            if (!_resources.IsReady)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _paintingTime += dt;
            Shader.SetGlobalFloat(PaintingTimeId, _paintingTime);

            bool stamped = FlushPendingStamps();
            bool decayed = TickDecay();

            // Reset wind dampen accumulator for next frame (min of all callers wins).
            _windDampen = 1f;

            if (!stamped && !decayed)
            {
                return;
            }
        }

        void IDisposable.Dispose()
        {
            Shader.SetGlobalFloat(ActiveId, 0f);
            _resources.Dispose();
        }

        /// <summary>Stamps a palette color at the given world position with the given radius.</summary>
        internal void Stamp(Vector3 worldPosition, float radius, int paletteIndex)
        {
            Stamp(worldPosition, worldPosition, radius, paletteIndex);
        }

        /// <summary>Stamps a capsule from <paramref name="prevWorldPosition"/> to <paramref name="worldPosition"/>.</summary>
        internal void Stamp(Vector3 worldPosition, Vector3 prevWorldPosition, float radius, int paletteIndex)
        {
            if (!_resources.IsReady || paletteIndex < 0 || paletteIndex >= _palette.Colors.Count)
            {
                return;
            }

            var color = _palette.Colors[paletteIndex].Color;
            _pendingStamps.Add(new PendingStamp
            {
                Center = _coords.WorldToUV(worldPosition),
                PrevCenter = _coords.WorldToUV(prevWorldPosition),
                Radius = _coords.WorldRadiusToUV(radius),
                Color = new Vector4(color.r, color.g, color.b, 1f)
            });
        }

        /// <summary>Contributes a wind dampen factor for this frame. The minimum of all callers is used.</summary>
        internal void SetWindDampen(float factor)
        {
            _windDampen = Mathf.Min(_windDampen, Mathf.Clamp01(factor));
        }

        private bool FlushPendingStamps()
        {
            if (_pendingStamps.Count == 0)
            {
                return false;
            }

            var mat = _resources.StampMaterial;
            if (mat == null)
            {
                _pendingStamps.Clear();
                return false;
            }

            int remaining = _pendingStamps.Count;
            int offset = 0;

            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, MaxStampsPerBatch);

                for (int i = 0; i < batch; i++)
                {
                    var s = _pendingStamps[offset + i];
                    _batchCenters[i] = new Vector4(s.Center.x, s.Center.y, s.PrevCenter.x, s.PrevCenter.y);
                    _batchRadii[i] = s.Radius;
                    _batchColors[i] = s.Color;
                }

                mat.SetInt(StampCountId, batch);
                mat.SetVectorArray(StampCentersId, _batchCenters);
                mat.SetFloatArray(StampRadiiId, _batchRadii);
                mat.SetVectorArray(StampColorsId, _batchColors);

                _resources.BlitAndSwap(mat);

                offset += batch;
                remaining -= batch;
            }

            _pendingStamps.Clear();
            return true;
        }

        private bool TickDecay()
        {
            if (_settings.DecayTickInterval > 0f &&
                Time.time - _lastDecayTime < _settings.DecayTickInterval)
            {
                return false;
            }

            _lastDecayTime = Time.time;

            var mat = _resources.DecayMaterial;
            if (mat == null)
            {
                return false;
            }

            float dt = _settings.DecayTickInterval > 0f ? _settings.DecayTickInterval : Time.deltaTime;
            _timePhase += dt;
            mat.SetFloat(DecayRateId, _settings.DecayRate);
            mat.SetFloat(DeltaTimeId, dt);
            mat.SetFloat(TimePhaseId, _timePhase);
            mat.SetFloat(WindSpeedId, _settings.WindSpeed * _settings.WindInfluence * _windDampen);
            mat.SetVector(WindDirId, _settings.WindDirection);
            mat.SetFloat(WindAgeBiasId, _settings.WindAgeBias);

            _resources.BlitAndSwap(mat);
            return true;
        }

        private void PushBoundsGlobals()
        {
            Shader.SetGlobalVector(BoundsMinId, new Vector4(_bounds.xMin, _bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(BoundsSizeId, new Vector4(_bounds.width, _bounds.height, 0f, 0f));
        }

        private struct PendingStamp
        {
            public Vector2 Center;
            public Vector2 PrevCenter;
            public float Radius;
            public Vector4 Color;
        }
    }
}

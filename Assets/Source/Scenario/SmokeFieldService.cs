using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Cadence;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     Owns the smoke field: a persistent screen-space RT that accumulates palette-colored stamps
    ///     and decays them over time. Publishes the RT as global shader properties
    ///     (<c>_SmokeTex</c>, <c>_SmokeBoundsMin/Size</c>) so any consumer can sample it.
    ///     Architecture mirrors <c>DisturbanceFieldService</c> / <c>SceneLightFieldService</c>:
    ///     plain-C# DI singleton, no MonoBehaviour, ping-pong blit pipeline. On a paper (bonus) level
    ///     (<see cref="IActiveScenario" />) a second field runs beside it, the ink: sources whose profile is on the
    ///     <see cref="PaintLayer.Ink" /> layer (pops, the Paint item) stamp there instead, wider, each newer stroke
    ///     covering what is under it, and never decay. It is published as <c>_InkFieldTex</c> (same bounds), and the
    ///     paper draws it into the page. The smoke itself is the same on paper as anywhere.
    /// </summary>
    internal sealed class SmokeFieldService : IStartable, ITickable, IDisposable, ICadencedEffect
    {
        // A smear is a few capsules end to end, each thinner than the one before.
        private const int SmearSegments = 3;
        private const float SmearTailRadius = 0.45f;
        private const int MaxStampsPerBatch = 32;

        private static readonly int BoundsMinId = Shader.PropertyToID("_SmokeBoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_SmokeBoundsSize");
        private static readonly int ActiveId = Shader.PropertyToID("_SmokeFieldActive");
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
        private static readonly int SmokeTimeId = Shader.PropertyToID("_SmokeTime");
        private static readonly int InkActiveId = Shader.PropertyToID("_InkFieldActive");
        private static readonly int InkTexelSizeId = Shader.PropertyToID("_InkFieldTexelSize");
        private static readonly int StampOverwriteId = Shader.PropertyToID("_StampOverwrite");
        private static readonly int StampAspectId = Shader.PropertyToID("_StampAspect");

        private readonly ISmokeFieldSettings _settings;
        private readonly IGameDisplayConfiguration _display;
        private readonly IGamePalette _palette;
        private readonly ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<RunResetMessage> _runResetSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionCompletedSubscriber;
        private readonly IActiveScenario _scenario;
        private readonly SmokeFieldResources _resources = new();
        private readonly SmokeFieldResources _inkResources = new("_InkFieldTex");
        private readonly List<PendingStamp> _pendingStamps = new();
        private readonly List<PendingStamp> _pendingInkStamps = new();
        private readonly Vector4[] _batchCenters = new Vector4[MaxStampsPerBatch];
        private readonly float[] _batchRadii = new float[MaxStampsPerBatch];
        private readonly Vector4[] _batchColors = new Vector4[MaxStampsPerBatch];

        private DisturbanceFieldCoordinates _coords;
        private Rect _bounds;
        private float _lastDecayTime;
        private float _timePhase;
        private float _smokeTime;
        private float _windDampen = 1f;
        private IDisposable _levelUpDismissedSubscription;
        private IDisposable _gameOverSubscription;
        private IDisposable _scenarioSubscription;
        private IDisposable _runResetSubscription;
        private IDisposable _transitionCompletedSubscription;

        // Whether ink-layer sources stamp: on paper, with the ink switched on.
        internal bool IsInking { get; private set; }

        public SmokeFieldService(
            ISmokeFieldSettings settings,
            IGameDisplayConfiguration display,
            IGamePalette palette,
            ISubscriber<LevelUpDismissedMessage> levelUpDismissedSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<RunResetMessage> runResetSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> transitionCompletedSubscriber,
            IActiveScenario scenario)
        {
            _settings = settings;
            _display = display;
            _palette = palette;
            _levelUpDismissedSubscriber = levelUpDismissedSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _runResetSubscriber = runResetSubscriber;
            _transitionCompletedSubscriber = transitionCompletedSubscriber;
            _scenario = scenario;
        }

        void IStartable.Start()
        {
            // Before the resources, so the ink switch follows the scenario even with the field disabled.
            _scenarioSubscription = _scenario.Current.Subscribe(shift => SetPaper(shift.Kind == ScenarioKind.Paper));

            // The ink stays on the page while it leaves: through the level-up's ascent and the swap out to the next
            // scenario, and under the game-over screen. It goes once the ascent has settled, when the page is gone
            // or, on a second paper level in a row, is a fresh page; and on a restart (below), which a pause-menu
            // restart publishes too, so the ink never carries over onto the retried page.
            _transitionCompletedSubscription = _transitionCompletedSubscriber.Subscribe(_ => ClearInk());

            if (_settings?.StampShader == null || _settings.DecayShader == null || _display == null)
            {
                Log.Warn("SmokeField", "disabled: assign stamp + decay shaders on SmokeFieldSettings.");
                return;
            }

            _coords = new DisturbanceFieldCoordinates(_display, _settings.TexelsPerUnit);
            _bounds = _coords.Bounds;
            _resources.Initialize(_settings, _coords.Width, _coords.Height);
            _inkResources.Initialize(_settings, _coords.Width, _coords.Height);
            ConfigureInkStamp();

            PushBoundsGlobals();
            Shader.SetGlobalFloat(ActiveId, 1f);
            _lastDecayTime = Time.time;
            _levelUpDismissedSubscription = _levelUpDismissedSubscriber.Subscribe(_ => ClearSmoke());
            _gameOverSubscription = _gameOverSubscriber.Subscribe(_ => ClearSmoke());
            _runResetSubscription = _runResetSubscriber.Subscribe(_ => Clear());
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

            _smokeTime += dt;
            Shader.SetGlobalFloat(SmokeTimeId, _smokeTime);

            FlushPendingStamps(_pendingStamps, _resources);
            FlushPendingStamps(_pendingInkStamps, _inkResources);
            TickDecay();

            // Reset wind dampen accumulator for next frame (min of all callers wins).
            _windDampen = 1f;
        }

        void IDisposable.Dispose()
        {
            _levelUpDismissedSubscription?.Dispose();
            _gameOverSubscription?.Dispose();
            _scenarioSubscription?.Dispose();
            _runResetSubscription?.Dispose();
            _transitionCompletedSubscription?.Dispose();
            Shader.SetGlobalFloat(ActiveId, 0f);
            Shader.SetGlobalFloat(InkActiveId, 0f);
            _resources.Dispose();
            _inkResources.Dispose();
        }

        int ICadencedEffect.BlitWeight => 1;

        void ICadencedEffect.ApplyPhaseOffset(float offset01)
        {
            // Decay uses Time.time - _lastDecayTime >= interval. Pre-advancing _lastDecayTime
            // backward by offset01 × interval delays the first fire by (1-offset01) × interval,
            // matching the accumulator semantics of the other services.
            if (_settings.DecayTickInterval > 0f)
            {
                _lastDecayTime = Time.time - offset01 * _settings.DecayTickInterval;
            }
        }

        /// <summary>Paints at the given world position using the source's profile. Pass <paramref name="paletteIndex"/>
        /// when the profile's <see cref="PaintColorMode"/> is <c>Palette</c>; ignored for <c>Custom</c>.</summary>
        internal void Paint(PaintSource source, Vector3 worldPosition, int paletteIndex = -1)
        {
            Paint(source, worldPosition, worldPosition, paletteIndex);
        }

        /// <summary>Paints a capsule from <paramref name="prevWorldPosition"/> to <paramref name="worldPosition"/>
        /// using the source's profile.</summary>
        internal void Paint(PaintSource source, Vector3 worldPosition, Vector3 prevWorldPosition,
            int paletteIndex = -1)
        {
            if (TryResolveStamp(source, paletteIndex, out var profile, out var color))
            {
                QueueStamp(profile, worldPosition, prevWorldPosition, profile.Radius, color);
            }
        }

        /// <summary>Drags the source's stroke back from <paramref name="worldPosition"/>, against
        /// <paramref name="direction"/>, for its profile's <see cref="PaintProfile.Smear"/>, thinning toward the
        /// tail: a flung blob of wet paint smearing where it lands. A zero direction leaves a round blot.</summary>
        internal void Smear(PaintSource source, Vector2 worldPosition, Vector2 direction, int paletteIndex = -1)
        {
            if (!TryResolveStamp(source, paletteIndex, out var profile, out var color))
            {
                return;
            }

            var tail = -direction.normalized * profile.Smear;
            for (var i = 0; i < SmearSegments; i++)
            {
                var from = worldPosition + tail * ((float)i / SmearSegments);
                var to = worldPosition + tail * ((float)(i + 1) / SmearSegments);
                var thinning = Mathf.Lerp(1f, SmearTailRadius, (float)i / (SmearSegments - 1));
                QueueStamp(profile, from, to, profile.Radius * thinning, color);
            }
        }

        /// <summary>Scatters <paramref name="count"/> stamps in a random ring of <paramref name="scatterRadius"/>
        /// around <paramref name="worldPosition"/>. Each stamp uses the source profile's radius, opacity, and color.
        /// Currently unused; reserved for future scatter sources.</summary>
        internal void PaintScatter(PaintSource source, Vector3 worldPosition, int count, float scatterRadius,
            int paletteIndex = -1)
        {
            if (count <= 0 || !TryResolveStamp(source, paletteIndex, out var profile, out var color))
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dist = UnityEngine.Random.Range(0f, scatterRadius);
                var offset = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
                var pos = worldPosition + offset;
                QueueStamp(profile, pos, pos, profile.Radius, color);
            }
        }

        /// <summary>Contributes a wind dampen factor for this frame. The minimum of all callers is used.</summary>
        internal void SetWindDampen(float factor)
        {
            _windDampen = Mathf.Min(_windDampen, Mathf.Clamp01(factor));
        }

        private bool TryResolveStamp(PaintSource source, int paletteIndex, out PaintProfile profile, out Vector4 color)
        {
            profile = default;
            color = default;
            if (!_resources.IsReady)
            {
                return false;
            }

            profile = _settings.GetProfile(source);
            var resolved = ResolveColor(profile, paletteIndex);
            if (!resolved.HasValue || (profile.Layer == PaintLayer.Ink && !IsInking))
            {
                return false;
            }

            var c = resolved.Value;
            color = new Vector4(c.r, c.g, c.b, Mathf.Clamp01(profile.Opacity));
            return true;
        }

        private void QueueStamp(PaintProfile profile, Vector3 worldPosition, Vector3 prevWorldPosition, float radius,
            Vector4 color)
        {
            var ink = profile.Layer == PaintLayer.Ink;
            var scale = ink ? Mathf.Max(1f, _settings.PaperStrokeScale) : 1f;
            (ink ? _pendingInkStamps : _pendingStamps).Add(new PendingStamp
            {
                Center = _coords.WorldToUV(worldPosition),
                PrevCenter = _coords.WorldToUV(prevWorldPosition),
                Radius = _coords.WorldRadiusToUV(radius * scale),
                Color = color
            });
        }

        private Color? ResolveColor(PaintProfile profile, int paletteIndex)
        {
            switch (profile.ColorMode)
            {
                case PaintColorMode.Custom:
                    return profile.CustomColor;

                case PaintColorMode.Palette:
                {
                    var idx = _palette.PaletteIndexOf(profile.PaletteColorName);
                    if (idx < 0 || idx >= _palette.Colors.Count)
                    {
                        return null;
                    }

                    return _palette.Colors[idx].Color;
                }

                default:
                {
                    if (paletteIndex < 0 || paletteIndex >= _palette.Colors.Count)
                    {
                        return null;
                    }

                    return _palette.Colors[paletteIndex].Color;
                }
            }
        }

        // The level clears the page as it always clears the smoke (the level-up dismissal, a game over, a restart),
        // so the ink is normally gone by the beat that brings a change. Leaving paper clears it anyway, so a later
        // page starts blank.
        private void SetPaper(bool paper)
        {
            if (_settings == null)
            {
                return;
            }

            // Leaving paper stops the stamping but keeps what is on the page, which still shows while the swap
            // takes it away; the ink clears once the transition settles (ClearInk).
            IsInking = paper && _settings.InkOnPaper;
            if (IsInking)
            {
                Shader.SetGlobalFloat(InkActiveId, 1f);
            }
        }

        // Ink always overwrites and measures round; the smoke's stamp material keeps both off.
        private void ConfigureInkStamp()
        {
            var mat = _inkResources.StampMaterial;
            if (mat == null)
            {
                return;
            }

            mat.SetFloat(StampOverwriteId, 1f);
            mat.SetFloat(StampAspectId, _bounds.width > 0.001f ? _bounds.height / _bounds.width : 1f);
        }

        private void Clear()
        {
            ClearSmoke();
            ClearInk();
        }

        private void ClearSmoke()
        {
            _pendingStamps.Clear();
            _resources.Clear();
        }

        private void ClearInk()
        {
            _pendingInkStamps.Clear();
            _inkResources.Clear();
            Shader.SetGlobalFloat(InkActiveId, IsInking ? 1f : 0f);
        }

        private void FlushPendingStamps(List<PendingStamp> pending, SmokeFieldResources target)
        {
            if (pending.Count == 0)
            {
                return;
            }

            var mat = target.StampMaterial;
            if (mat == null)
            {
                pending.Clear();
                return;
            }

            int remaining = pending.Count;
            int offset = 0;

            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, MaxStampsPerBatch);

                for (int i = 0; i < batch; i++)
                {
                    var s = pending[offset + i];
                    _batchCenters[i] = new Vector4(s.Center.x, s.Center.y, s.PrevCenter.x, s.PrevCenter.y);
                    _batchRadii[i] = s.Radius;
                    _batchColors[i] = s.Color;
                }

                mat.SetInt(StampCountId, batch);
                mat.SetVectorArray(StampCentersId, _batchCenters);
                mat.SetFloatArray(StampRadiiId, _batchRadii);
                mat.SetVectorArray(StampColorsId, _batchColors);

                target.BlitAndSwap(mat);

                offset += batch;
                remaining -= batch;
            }

            pending.Clear();
        }

        private void TickDecay()
        {
            if (_settings.DecayTickInterval > 0f &&
                Time.time - _lastDecayTime < _settings.DecayTickInterval)
            {
                return;
            }

            _lastDecayTime = Time.time;

            var mat = _resources.DecayMaterial;
            if (mat == null)
            {
                return;
            }

            float dt = _settings.DecayTickInterval > 0f ? _settings.DecayTickInterval : Time.deltaTime;
            _timePhase += dt;
            mat.SetFloat(DecayRateId, _settings.DecayRate);
            mat.SetFloat(DeltaTimeId, dt);
            mat.SetFloat(TimePhaseId, _timePhase);
            mat.SetFloat(WindSpeedId, _settings.WindSpeed * _settings.WindInfluence * _windDampen);
            mat.SetVector(WindDirId, ComputeSwungWindDirection());
            mat.SetFloat(WindAgeBiasId, _settings.WindAgeBias);

            _resources.BlitAndSwap(mat);
        }

        private void PushBoundsGlobals()
        {
            Shader.SetGlobalVector(BoundsMinId, new Vector4(_bounds.xMin, _bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(BoundsSizeId, new Vector4(_bounds.width, _bounds.height, 0f, 0f));
            Shader.SetGlobalVector(InkTexelSizeId, new Vector4(1f / _coords.Width, 1f / _coords.Height, 0f, 0f));
        }

        private Vector4 ComputeSwungWindDirection()
        {
            var baseDir = _settings.WindDirection;
            float swingAngle = _settings.WindSwingAngle;
            if (swingAngle <= 0f)
            {
                return baseDir;
            }

            float t = Mathf.Sin(_smokeTime * _settings.WindSwingSpeed * Mathf.PI * 2f);
            float angleDeg = t * swingAngle;
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector4(
                baseDir.x * cos - baseDir.y * sin,
                baseDir.x * sin + baseDir.y * cos,
                0f, 0f);
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

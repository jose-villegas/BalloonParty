using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using Random = UnityEngine.Random;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     A GPU-simulated field of ambient specks (dust/pollen). Each frame a compute pass advects every
    ///     active speck by Brownian drift + the scenario's motion vector + the disturbance field, wrapping
    ///     them toroidally within a world region; they render as billboards straight from the GPU buffer
    ///     (no CPU readback). The motion vector is the smoothed velocity of <see cref="ScenarioContentRoot" />,
    ///     so the field reacts to every scenario beat — the ascend, the restart descent, the float-away —
    ///     automatically. The buffer size is a cap: the field builds up as balloon pops enable specks
    ///     (each burst seeded at the pop point), rather than all being present from the start. A reduction
    ///     curve runs continuously, lowering the active ceiling to drain the field; every burst restarts it
    ///     from zero (snapping the ceiling back up) — so a flurry of pops keeps the field full and reads as
    ///     chaos, a lull lets it thin back down.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    internal sealed class SpeckField : MonoBehaviour
    {
        private const int ThreadGroupSize = 64;
        private const int SpeckStrideBytes = sizeof(float) * 13;
        private const int MaxPaletteSlots = 16;

        private static readonly int SpecksId = Shader.PropertyToID("_Specks");
        private static readonly int CountId = Shader.PropertyToID("_Count");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimeId = Shader.PropertyToID("_TimeSeconds");
        private static readonly int MotionDeltaId = Shader.PropertyToID("_MotionDelta");
        private static readonly int BrownianStrengthId = Shader.PropertyToID("_BrownianStrength");
        private static readonly int DragId = Shader.PropertyToID("_Drag");
        private static readonly int MotionInfluenceId = Shader.PropertyToID("_MotionInfluence");
        private static readonly int DisturbanceInfluenceId = Shader.PropertyToID("_DisturbanceInfluence");
        private static readonly int DisturbanceDampingId = Shader.PropertyToID("_DisturbanceDamping");
        private static readonly int SwirlAngleId = Shader.PropertyToID("_SwirlAngle");
        private static readonly int FlowInfluenceId = Shader.PropertyToID("_FlowInfluence");
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int FieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int RegionMinId = Shader.PropertyToID("_RegionMin");
        private static readonly int RegionSizeId = Shader.PropertyToID("_RegionSize");
        private static readonly int MinLifetimeId = Shader.PropertyToID("_MinLifetime");
        private static readonly int MaxLifetimeId = Shader.PropertyToID("_MaxLifetime");
        private static readonly int SpeckSizeId = Shader.PropertyToID("_SpeckSize");
        private static readonly int TrailLengthId = Shader.PropertyToID("_TrailLength");
        private static readonly int TrailMaxId = Shader.PropertyToID("_TrailMax");
        private static readonly int MinScaleId = Shader.PropertyToID("_MinScale");
        private static readonly int MaxScaleId = Shader.PropertyToID("_MaxScale");
        private static readonly int ScalePulseSpeedId = Shader.PropertyToID("_ScalePulseSpeed");
        private static readonly int SpeckTimeId = Shader.PropertyToID("_SpeckTime");
        private static readonly int FadeInId = Shader.PropertyToID("_FadeIn");
        private static readonly int FadeOutId = Shader.PropertyToID("_FadeOut");
        private static readonly int HeatGainId = Shader.PropertyToID("_HeatGain");
        private static readonly int HeatDecayId = Shader.PropertyToID("_HeatDecay");
        private static readonly int ColorLerpRateId = Shader.PropertyToID("_ColorLerpRate");
        private static readonly int SpeckPaletteId = Shader.PropertyToID("_SpeckPalette");
        private static readonly int SpeckPaletteCountId = Shader.PropertyToID("_SpeckPaletteCount");
        private static readonly int ActiveCountId = Shader.PropertyToID("_ActiveCount");
        private static readonly int SpeckLookAId = Shader.PropertyToID("_SpeckLookA");
        private static readonly int SpeckLookBId = Shader.PropertyToID("_SpeckLookB");
        private static readonly int SpeckLookCId = Shader.PropertyToID("_SpeckLookC");
        private static readonly int SpeckLookCountId = Shader.PropertyToID("_SpeckLookCount");
        private static readonly int SpeckMotionAId = Shader.PropertyToID("_SpeckMotionA");
        private static readonly int SpeckMotionBId = Shader.PropertyToID("_SpeckMotionB");
        private static readonly int SpeckMotionCountId = Shader.PropertyToID("_SpeckMotionCount");

        [SerializeField] private ComputeShader _compute;
        [SerializeField] private Material _renderMaterial;

        [Inject] private ISpeckFieldSettings _settings;
        [Inject] private ScenarioContentRoot _scenarioRoot;
        [Inject] private DisturbanceFieldService _disturbance;
        [Inject] private IGamePalette _palette;
        [Inject] private ISubscriber<ActorHitMessage> _hitSubscriber;
        [Inject] private ISubscriber<SpeckSpawnRequestMessage> _requestSubscriber;

        private readonly List<PendingSpeck> _pending = new();

        private ComputeBuffer _speckBuffer;
        private Mesh _mesh;
        private int _count;
        private int _kernel;
        private Vector2 _lastRootPos;
        private Vector2 _motionDelta;
        private bool _motionSeeded;
        private bool _ready;
        private int _activeCount;
        private int _ceiling;
        private float _reductionElapsed;
        private Speck[] _burst;
        private Vector4[] _lookA;
        private Vector4[] _lookB;
        private Vector4[] _lookC;
        private int _lookCount;
        private Vector4[] _motionA;
        private Vector4[] _motionB;
        private int _motionCount;
        private IDisposable _hitSubscription;
        private IDisposable _requestSubscription;

        private void Start()
        {
            if (_compute == null || _renderMaterial == null || _settings == null || _settings.Count <= 0)
            {
                enabled = false;
                return;
            }

            _count = _settings.Count;

            // The sim needs compute; the render reads the speck buffer in the vertex stage. Both are gated
            // by the graphics API — GLES3.0/2.0 lack them, so a mobile build must use Vulkan or Metal.
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("SpeckField disabled: compute shaders unsupported (graphics API — use Vulkan/Metal).", this);
                enabled = false;
                return;
            }

            if (SystemInfo.maxComputeBufferInputsVertex < 1)
            {
                Debug.LogWarning("SpeckField disabled: the vertex stage can't read compute buffers on this " +
                    "device/API (use Vulkan/Metal, or a texture-based fallback).", this);
                enabled = false;
                return;
            }

            _kernel = _compute.FindKernel("Advect");
            _speckBuffer = new ComputeBuffer(_count, SpeckStrideBytes);
            SeedSpecks();

            // Render through a MeshRenderer (a dummy count*6-vertex mesh; the vertex shader repositions each
            // vert from the buffer) so the field goes through normal sprite sorting — set the MeshRenderer's
            // Sorting Layer / Order to sit under the UI but over the background.
            BuildRenderMesh();

            _lookA = new Vector4[MaxPaletteSlots];
            _lookB = new Vector4[MaxPaletteSlots];
            _lookC = new Vector4[MaxPaletteSlots];
            _motionA = new Vector4[MaxPaletteSlots];
            _motionB = new Vector4[MaxPaletteSlots];

            PushPalette();
            PushStaticParams();

            _burst = new Speck[Mathf.Clamp(MaxProfileCount(), 1, _count)];
            _activeCount = _settings.Spawning.SpawnAllImmediately ? _count : Mathf.Clamp(_settings.Spawning.InitialActiveCount, 0, _count);
            _ceiling = _count;

            // Pops (and explicit spawn requests) enable bursts and restart the reduction curve; between
            // bursts the curve drains the field — so the balance of spawns vs the drain is the feedback.
            _hitSubscription = _hitSubscriber.Subscribe(OnActorHit);
            _requestSubscription = _requestSubscriber.Subscribe(OnSpawnRequest);

            _ready = true;
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                return;
            }

            // Scaled time: the field slows with slow-mo and freezes at timeScale 0 (e.g. the level-up
            // popup), matching the game. dt == 0 (paused) skips the sim, holding the specks in place.
            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

#if UNITY_EDITOR
            // Re-push the constant params each frame in the editor so inspector tweaks preview live; a build
            // pushes them once in Start.
            PushStaticParams();
#endif

            // A burst this frame restarts the reduction curve before the ceiling is recomputed, so the spawn
            // fills toward a refreshed (full) ceiling rather than the just-drained one.
            if (_pending.Count > 0)
            {
                _reductionElapsed = 0f;
            }

            UpdateReduction(dt);
            FlushPops();
            SampleMotionDelta();
            Dispatch(dt);
            PushRenderParams();
        }

        private void OnDestroy()
        {
            _hitSubscription?.Dispose();
            _hitSubscription = null;
            _requestSubscription?.Dispose();
            _requestSubscription = null;
            _speckBuffer?.Release();
            _speckBuffer = null;
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        private void BuildRenderMesh()
        {
            _mesh = new Mesh { name = "SpeckField", indexFormat = IndexFormat.UInt32 };

            var vertexCount = _count * 6;
            var vertices = new Vector3[vertexCount];
            var indices = new int[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                indices[i] = i;
            }

            _mesh.SetVertices(vertices);
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            // Verts sit at the origin (the shader repositions them); huge bounds so spread-out specks never cull.
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshRenderer>().sharedMaterial = _renderMaterial;
        }

        // The per-frame material state (only what actually changes each frame); runs in LateUpdate, before the
        // camera renders. The constants live in PushStaticParams.
        private void PushRenderParams()
        {
            _renderMaterial.SetFloat(SpeckTimeId, Time.time);
            _renderMaterial.SetInt(ActiveCountId, _activeCount);
        }

        // Compute + material params that never change at runtime — pushed once in Start (and every frame in the
        // editor for live inspector tuning). Setting them on the shared compute/material assets persists across
        // dispatches, so per-frame re-pushing them was redundant.
        private void PushStaticParams()
        {
            var motion = _settings.Motion;
            var look = _settings.Appearance;
            var region = _settings.RegionSize;
            var lifetime = look.LifetimeRange;
            var scale = look.ScaleRange;

            _compute.SetBuffer(_kernel, SpecksId, _speckBuffer);
            _compute.SetFloat(BrownianStrengthId, motion.BrownianStrength);
            _compute.SetFloat(DragId, motion.Drag);
            _compute.SetFloat(MotionInfluenceId, motion.MotionInfluence);
            _compute.SetVector(RegionMinId, region * -0.5f);
            _compute.SetVector(RegionSizeId, region);
            _compute.SetFloat(MinLifetimeId, lifetime.x);
            _compute.SetFloat(MaxLifetimeId, lifetime.y);
            _compute.SetFloat(DisturbanceDampingId, motion.DisturbanceDamping);
            _compute.SetVector(SwirlAngleId, motion.SwirlAngle * Mathf.Deg2Rad);
            _compute.SetFloat(FlowInfluenceId, motion.FlowInfluence);
            _compute.SetFloat(HeatGainId, look.HeatGain);
            _compute.SetFloat(HeatDecayId, look.HeatDecay);
            _compute.SetFloat(ColorLerpRateId, look.ColorLerpRate);

            _renderMaterial.SetBuffer(SpecksId, _speckBuffer);
            _renderMaterial.SetFloat(SpeckSizeId, look.SpeckSize);
            _renderMaterial.SetFloat(TrailLengthId, look.TrailLength);
            _renderMaterial.SetFloat(TrailMaxId, look.TrailMax);
            _renderMaterial.SetFloat(MinScaleId, scale.x);
            _renderMaterial.SetFloat(MaxScaleId, scale.y);
            _renderMaterial.SetVector(ScalePulseSpeedId, look.ScalePulseSpeed);
            _renderMaterial.SetFloat(FadeInId, look.FadeIn);
            _renderMaterial.SetFloat(FadeOutId, look.FadeOut);

            BuildLookArrays(look);
            _renderMaterial.SetVectorArray(SpeckLookAId, _lookA);
            _renderMaterial.SetVectorArray(SpeckLookBId, _lookB);
            _renderMaterial.SetVectorArray(SpeckLookCId, _lookC);
            _renderMaterial.SetInt(SpeckLookCountId, _lookCount);

            BuildMotionArrays(motion);
            _compute.SetVectorArray(SpeckMotionAId, _motionA);
            _compute.SetVectorArray(SpeckMotionBId, _motionB);
            _compute.SetInt(SpeckMotionCountId, _motionCount);
        }

        // Per-colour motion overrides resolved into palette-slot arrays the compute lerps toward by a speck's
        // heat — the motion analogue of BuildLookArrays. Swirl is stored in radians to match the base uniform.
        // Packed A=(brownian, drag, motionInfluence, disturbanceInfluence), B=(damping, swirlMin, swirlMax, flow).
        private void BuildMotionArrays(ISpeckMotionSettings motion)
        {
            _motionCount = Mathf.Min(_palette.Colors.Count, MaxPaletteSlots);

            var baseA = new Vector4(motion.BrownianStrength, motion.Drag, motion.MotionInfluence, motion.DisturbanceInfluence);
            var baseB = new Vector4(motion.DisturbanceDamping, motion.SwirlAngle.x * Mathf.Deg2Rad,
                motion.SwirlAngle.y * Mathf.Deg2Rad, motion.FlowInfluence);

            var profiles = motion.ColorProfiles;
            for (var slot = 0; slot < _motionCount; slot++)
            {
                var a = baseA;
                var b = baseB;

                for (var p = 0; p < profiles.Count; p++)
                {
                    var profile = profiles[p];
                    if ((profile.ColorMask & (1 << slot)) == 0)
                    {
                        continue;
                    }

                    a = new Vector4(profile.BrownianStrength, profile.Drag, profile.MotionInfluence, profile.DisturbanceInfluence);
                    b = new Vector4(profile.DisturbanceDamping, profile.SwirlAngle.x * Mathf.Deg2Rad,
                        profile.SwirlAngle.y * Mathf.Deg2Rad, profile.FlowInfluence);
                    break;
                }

                _motionA[slot] = a;
                _motionB[slot] = b;
            }
        }

        // Resolves the per-colour look overrides into palette-slot-indexed arrays the render shader lerps
        // toward by a speck's heat. Every slot starts at the base look, so uncovered colours are a no-op;
        // a profile whose colour resolves to a slot overwrites it. Packed three Vector4s per slot:
        // A=(size, trailLength, trailMax, fadeIn), B=(fadeOut, minScale, maxScale, pulseMin), C=(pulseMax…).
        private void BuildLookArrays(ISpeckAppearanceSettings look)
        {
            _lookCount = Mathf.Min(_palette.Colors.Count, MaxPaletteSlots);

            var baseA = new Vector4(look.SpeckSize, look.TrailLength, look.TrailMax, look.FadeIn);
            var baseB = new Vector4(look.FadeOut, look.ScaleRange.x, look.ScaleRange.y, look.ScalePulseSpeed.x);
            var baseC = new Vector4(look.ScalePulseSpeed.y, 0f, 0f, 0f);

            var profiles = look.ColorProfiles;
            for (var slot = 0; slot < _lookCount; slot++)
            {
                var a = baseA;
                var b = baseB;
                var c = baseC;

                // First profile whose mask covers this colour wins; uncovered colours keep the base look.
                for (var p = 0; p < profiles.Count; p++)
                {
                    var profile = profiles[p];
                    if ((profile.ColorMask & (1 << slot)) == 0)
                    {
                        continue;
                    }

                    a = new Vector4(profile.SpeckSize, profile.TrailLength, profile.TrailMax, profile.FadeIn);
                    b = new Vector4(profile.FadeOut, profile.ScaleRange.x, profile.ScaleRange.y, profile.ScalePulseSpeed.x);
                    c = new Vector4(profile.ScalePulseSpeed.y, 0f, 0f, 0f);
                    break;
                }

                _lookA[slot] = a;
                _lookB[slot] = b;
                _lookC[slot] = c;
            }
        }

        // The palette the render lerps disturbed specks toward; indices must match the stampers'
        // (IGamePalette.Colors order — the same mapping DisturbanceFieldService encodes into the field).
        private void PushPalette()
        {
            var colors = new Vector4[MaxPaletteSlots];
            var count = Mathf.Min(_palette.Colors.Count, MaxPaletteSlots);
            for (var i = 0; i < count; i++)
            {
                colors[i] = _palette.Colors[i].Color;
            }

            _renderMaterial.SetVectorArray(SpeckPaletteId, colors);
            _renderMaterial.SetInt(SpeckPaletteCountId, count);
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Outcome == HitOutcome.Pop)
            {
                Enqueue(GetProfile(SpeckSource.BalloonPop), msg.WorldPosition);
            }
        }

        private void OnSpawnRequest(SpeckSpawnRequestMessage msg)
        {
            Enqueue(GetProfile(msg.Source), msg.WorldPosition);
        }

        private void Enqueue(SpeckProfile profile, Vector2 worldPos)
        {
            if (profile.Count <= 0)
            {
                return;
            }

            _pending.Add(new PendingSpeck { Position = worldPos, Count = profile.Count, Spread = profile.Spread });
        }

        private SpeckProfile GetProfile(SpeckSource source)
        {
            var profiles = _settings.Spawning.SpeckProfiles;
            for (var i = 0; i < profiles.Count; i++)
            {
                if ((profiles[i].Sources & source) != 0)
                {
                    return profiles[i];
                }
            }

            return default;
        }

        private int MaxProfileCount()
        {
            var profiles = _settings.Spawning.SpeckProfiles;
            var max = 1;
            for (var i = 0; i < profiles.Count; i++)
            {
                max = Mathf.Max(max, profiles[i].Count);
            }

            return max;
        }

        // The reduction curve runs continuously (its X is seconds since the last burst, so its last key sets
        // the span; Evaluate holds past it): the ceiling follows it and clamps the active count down, draining
        // the field. A burst restarts the curve (see LateUpdate), snapping the ceiling back up.
        private void UpdateReduction(float dt)
        {
            // Testing mode: the field stays full — no drain, no clamp.
            if (_settings.Spawning.SpawnAllImmediately)
            {
                _ceiling = _count;
                return;
            }

            _reductionElapsed += dt;
            var frac = Mathf.Clamp01(_settings.Spawning.ReductionCurve.Evaluate(_reductionElapsed));
            _ceiling = Mathf.RoundToInt(frac * _count);
            _activeCount = Mathf.Min(_activeCount, _ceiling);
        }

        // Drained in LateUpdate so the buffer writes land before the frame's compute dispatch.
        private void FlushPops()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            foreach (var spawn in _pending)
            {
                EnableBurst(spawn.Position, spawn.Count, spawn.Spread);
            }

            _pending.Clear();
        }

        // Enables a burst of specks at a request point — written at the top of the active range and grown
        // into, clamped to the ceiling's remaining room (nothing when already at the ceiling). Specks are
        // world-space, so the request's world position places them directly; they pick up the colour from
        // the disturbance field's tag where they land.
        private void EnableBurst(Vector2 worldPos, int count, float spread)
        {
            var n = Mathf.Min(Mathf.Min(count, _burst.Length), _ceiling - _activeCount);
            if (n <= 0)
            {
                return;
            }

            for (var i = 0; i < n; i++)
            {
                _burst[i] = new Speck
                {
                    Position = worldPos + Random.insideUnitCircle * spread,
                    Velocity = Vector2.zero,
                    Seed = Random.value,
                    Age = 0f,
                    Lifetime = RandomLifetime(),
                    EffectiveVel = Vector2.zero,
                    Heat = 0f,
                    PaletteIndex = -1f,
                    PrevPaletteIndex = -1f,
                    ColorBlend = 1f,
                };
            }

            _speckBuffer.SetData(_burst, 0, _activeCount, n);
            _activeCount += n;
        }

        private float RandomLifetime()
        {
            var range = _settings.Appearance.LifetimeRange;
            return Mathf.Lerp(range.x, range.y, Random.value);
        }

        private void SeedSpecks()
        {
            var specks = new Speck[_count];
            var region = _settings.RegionSize;
            var min = region * -0.5f;
            for (var i = 0; i < _count; i++)
            {
                var lifetime = RandomLifetime();
                specks[i] = new Speck
                {
                    Position = new Vector2(
                        min.x + Random.value * region.x,
                        min.y + Random.value * region.y),
                    Velocity = Vector2.zero,
                    Seed = Random.value,
                    // Stagger the starting age across each lifetime so they don't all pop in together.
                    Age = Random.value * lifetime,
                    Lifetime = lifetime,
                    EffectiveVel = Vector2.zero,
                    Heat = 0f,
                    PaletteIndex = -1f,
                    PrevPaletteIndex = -1f,
                    ColorBlend = 1f,
                };
            }

            _speckBuffer.SetData(specks);
        }

        // The exact per-frame move of the content root; specks translate by this (x influence) to match
        // the scenario's motion 1:1 during a travel.
        private void SampleMotionDelta()
        {
            // Injection can lag this component's Start; until the root resolves, nothing to match.
            if (_scenarioRoot?.Transform == null)
            {
                _motionSeeded = false;
                _motionDelta = Vector2.zero;
                return;
            }

            var pos = RootPosition();
            var delta = pos - _lastRootPos;
            _lastRootPos = pos;

            if (!_motionSeeded)
            {
                _motionSeeded = true;
                _motionDelta = Vector2.zero;
                return;
            }

            // Reject teleports (the Ascent snaps the root to its start height on frame 0) — match only
            // real per-frame movement, not the snap.
            _motionDelta = delta.magnitude <= _settings.Motion.TeleportThreshold ? delta : Vector2.zero;
        }

        private Vector2 RootPosition()
        {
            var p = _scenarioRoot.Transform.position;
            return new Vector2(p.x, p.y);
        }

        private void Dispatch(float dt)
        {
            if (_activeCount <= 0)
            {
                return;
            }

            // _Count drives the compute's bounds guard; feeding the active count simulates only the
            // enabled specks (and dispatches just enough thread groups for them). Only the per-frame-varying
            // params are set here; the constants are pushed once by PushStaticParams.
            _compute.SetInt(CountId, _activeCount);
            _compute.SetFloat(DeltaTimeId, dt);
            _compute.SetFloat(TimeId, Time.time);
            _compute.SetVector(MotionDeltaId, _motionDelta);

            var hasField = _disturbance != null && _disturbance.FieldTexture != null;
            _compute.SetFloat(DisturbanceInfluenceId, hasField ? _settings.Motion.DisturbanceInfluence : 0f);
            _compute.SetTexture(_kernel, DisturbanceTexId, hasField ? _disturbance.FieldTexture : Texture2D.blackTexture);
            _compute.SetVector(FieldBoundsMinId, hasField ? _disturbance.FieldBoundsMin : Vector2.zero);
            _compute.SetVector(FieldBoundsSizeId, hasField ? _disturbance.FieldBoundsSize : Vector2.one);

            _compute.Dispatch(_kernel, Mathf.CeilToInt(_activeCount / (float)ThreadGroupSize), 1, 1);
        }

        private struct PendingSpeck
        {
            public Vector2 Position;
            public int Count;
            public float Spread;
        }

        private struct Speck
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Seed;
            public float Age;
            public float Lifetime;
            public Vector2 EffectiveVel;
            public float Heat;
            public float PaletteIndex;
            public float PrevPaletteIndex;
            public float ColorBlend;
        }
    }
}

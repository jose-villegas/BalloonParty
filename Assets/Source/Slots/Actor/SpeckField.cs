using System;
using System.Collections.Generic;
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
    ///     (each burst seeded at the pop point), rather than all being present from the start.
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

        [SerializeField] private ComputeShader _compute;
        [SerializeField] private Material _renderMaterial;
        [SerializeField] private int _count = 4096;
        [SerializeField] private Vector2 _regionSize = new(30f, 20f);
        [SerializeField] private float _brownianStrength = 0.6f;
        [SerializeField] private float _drag = 2f;
        [SerializeField] private float _motionInfluence = 1f;
        [SerializeField] private float _disturbanceInfluence = 1f;

        [Tooltip("Extra velocity damping applied where the disturbance is active, so the push settles.")]
        [SerializeField] private float _disturbanceDamping = 4f;

        [Tooltip("Per-speck swirl angle range (degrees) rotating the disturbance push — 0 = straight out, " +
                 "90 = pure orbit. Same rotational sense for all (a coherent vortex); each picks a random " +
                 "angle in this range.")]
        [SerializeField] private Vector2 _swirlAngle = new(30f, 90f);

        [Tooltip("Speed specks advance along the disturbance's own motion (the white direction), so the " +
                 "vortex travels with the flow. Bounded advection — high values go faster but don't run away.")]
        [SerializeField] private float _flowInfluence = 1f;

        [SerializeField] private float _speckSize = 0.03f;

        [Tooltip("Stretches each speck into a streak along its motion, scaled by speed. 0 = round dots.")]
        [SerializeField] private float _trailLength;

        [Tooltip("Max streak length added by the trail (world units), so a fast ascend can't over-stretch.")]
        [SerializeField] private float _trailMax = 0.5f;

        [Tooltip("Per-speck lifetime range (seconds). Each speck fades in, lives, fades out, then respawns.")]
        [SerializeField] private Vector2 _lifetimeRange = new(2f, 6f);

        [Tooltip("Per-speck scale multiplier range on _speckSize, oscillated over time for size variety.")]
        [SerializeField] private Vector2 _scaleRange = new(0.5f, 1.5f);

        [Tooltip("Per-speck scale-oscillation rate range; each speck picks a random speed in it, so they " +
                 "pulse out of sync (fake toward/away drift).")]
        [SerializeField] private Vector2 _scalePulseSpeed = new(0.4f, 1f);

        [Tooltip("Fraction of life spent fading/scaling in.")]
        [Range(0f, 0.5f)] [SerializeField] private float _fadeIn = 0.15f;

        [Tooltip("Fraction of life spent fading/scaling out.")]
        [Range(0f, 0.5f)] [SerializeField] private float _fadeOut = 0.25f;

        [Tooltip("Per-frame root move (world units) above which it's treated as a teleport (e.g. the " +
                 "Ascent snapping the root to its start height) and ignored, not matched.")]
        [SerializeField] private float _teleportThreshold = 1f;

        [Tooltip("How fast a disturbed speck heats toward the material's Disturbed Tint, per unit agitation per second. 0 = no tinting.")]
        [SerializeField] private float _heatGain = 4f;

        [Tooltip("How fast the heat cools once the disturbance passes, per second — the return to the base color.")]
        [SerializeField] private float _heatDecay = 1.5f;

        [Tooltip("Per-second ramp of a speck's crossfade when its palette tag changes color (e.g. the rainbow cycling). Higher = snappier; ~4 crossfades in a quarter second.")]
        [SerializeField] private float _colorLerpRate = 4f;

        [Header("Pop activation")]
        [Tooltip("Specks rendered at the start. The buffer size (Count) is the cap; each pop enables more " +
                 "up to it. 0 = the field starts empty and builds entirely from pops.")]
        [SerializeField] private int _initialActiveCount;

        [Tooltip("Specks a single balloon pop enables (once the cap is reached, it repositions this many " +
                 "of the oldest specks to the new pop instead).")]
        [SerializeField] private int _specksPerPop = 32;

        [Tooltip("World-space radius the enabled specks scatter within around the pop point.")]
        [SerializeField] private float _popSpread = 0.4f;

        [Inject] private ScenarioContentRoot _scenarioRoot;
        [Inject] private DisturbanceFieldService _disturbance;
        [Inject] private IGamePalette _palette;
        [Inject] private ISubscriber<ActorHitMessage> _hitSubscriber;

        private readonly List<Vector2> _pendingPops = new();

        private ComputeBuffer _speckBuffer;
        private Mesh _mesh;
        private int _kernel;
        private Vector2 _lastRootPos;
        private Vector2 _motionDelta;
        private bool _motionSeeded;
        private bool _ready;
        private int _activeCount;
        private int _writeCursor;
        private Speck[] _burst;
        private IDisposable _hitSubscription;

        private void Start()
        {
            if (_compute == null || _renderMaterial == null || _count <= 0)
            {
                enabled = false;
                return;
            }

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

            PushPalette();

            _specksPerPop = Mathf.Clamp(_specksPerPop, 1, _count);
            _burst = new Speck[_specksPerPop];
            _activeCount = Mathf.Clamp(_initialActiveCount, 0, _count);
            _writeCursor = _activeCount % _count;

            // Each pop enables (or, once capped, repositions) a burst of specks at the pop point.
            _hitSubscription = _hitSubscriber.Subscribe(OnActorHit);

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

            FlushPops();
            SampleMotionDelta();
            Dispatch(dt);
            PushRenderParams();
        }

        private void OnDestroy()
        {
            _hitSubscription?.Dispose();
            _hitSubscription = null;
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

        // Refresh the material state the MeshRenderer draws with; runs in LateUpdate, before the camera renders.
        private void PushRenderParams()
        {
            _renderMaterial.SetBuffer(SpecksId, _speckBuffer);
            _renderMaterial.SetFloat(SpeckSizeId, _speckSize);
            _renderMaterial.SetFloat(TrailLengthId, _trailLength);
            _renderMaterial.SetFloat(TrailMaxId, _trailMax);
            _renderMaterial.SetFloat(MinScaleId, _scaleRange.x);
            _renderMaterial.SetFloat(MaxScaleId, _scaleRange.y);
            _renderMaterial.SetVector(ScalePulseSpeedId, _scalePulseSpeed);
            _renderMaterial.SetFloat(SpeckTimeId, Time.time);
            _renderMaterial.SetFloat(FadeInId, _fadeIn);
            _renderMaterial.SetFloat(FadeOutId, _fadeOut);
            _renderMaterial.SetInt(ActiveCountId, _activeCount);
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
                _pendingPops.Add(msg.WorldPosition);
            }
        }

        // Drained in LateUpdate so the buffer writes land before the frame's compute dispatch.
        private void FlushPops()
        {
            if (_pendingPops.Count == 0)
            {
                return;
            }

            foreach (var pop in _pendingPops)
            {
                EnableBurst(pop);
            }

            _pendingPops.Clear();
        }

        // Fills the next burst of specks at the pop point, wrapping the write cursor over the buffer and
        // growing the active count toward the cap. Once capped, this repositions the oldest specks for a
        // fresh burst. Specks are world-space, so the pop's world position places them directly; they pick
        // up the pop's colour from the disturbance field's tag where they land.
        private void EnableBurst(Vector2 worldPos)
        {
            var n = _burst.Length;
            for (var i = 0; i < n; i++)
            {
                _burst[i] = new Speck
                {
                    Position = worldPos + Random.insideUnitCircle * _popSpread,
                    Velocity = Vector2.zero,
                    Seed = Random.value,
                    Age = 0f,
                    Lifetime = Mathf.Lerp(_lifetimeRange.x, _lifetimeRange.y, Random.value),
                    EffectiveVel = Vector2.zero,
                    Heat = 0f,
                    PaletteIndex = -1f,
                    PrevPaletteIndex = -1f,
                    ColorBlend = 1f,
                };
            }

            var firstSpan = Mathf.Min(n, _count - _writeCursor);
            _speckBuffer.SetData(_burst, 0, _writeCursor, firstSpan);
            if (firstSpan < n)
            {
                _speckBuffer.SetData(_burst, firstSpan, 0, n - firstSpan);
            }

            _writeCursor = (_writeCursor + n) % _count;
            _activeCount = Mathf.Min(_count, _activeCount + n);
        }

        private void SeedSpecks()
        {
            var specks = new Speck[_count];
            var min = _regionSize * -0.5f;
            for (var i = 0; i < _count; i++)
            {
                var lifetime = Mathf.Lerp(_lifetimeRange.x, _lifetimeRange.y, Random.value);
                specks[i] = new Speck
                {
                    Position = new Vector2(
                        min.x + Random.value * _regionSize.x,
                        min.y + Random.value * _regionSize.y),
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
            _motionDelta = delta.magnitude <= _teleportThreshold ? delta : Vector2.zero;
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
            // enabled specks (and dispatches just enough thread groups for them).
            _compute.SetInt(CountId, _activeCount);
            _compute.SetFloat(DeltaTimeId, dt);
            _compute.SetFloat(TimeId, Time.time);
            _compute.SetVector(MotionDeltaId, _motionDelta);
            _compute.SetFloat(BrownianStrengthId, _brownianStrength);
            _compute.SetFloat(DragId, _drag);
            _compute.SetFloat(MotionInfluenceId, _motionInfluence);
            _compute.SetVector(RegionMinId, _regionSize * -0.5f);
            _compute.SetVector(RegionSizeId, _regionSize);
            _compute.SetFloat(MinLifetimeId, _lifetimeRange.x);
            _compute.SetFloat(MaxLifetimeId, _lifetimeRange.y);

            var hasField = _disturbance != null && _disturbance.FieldTexture != null;
            _compute.SetFloat(DisturbanceInfluenceId, hasField ? _disturbanceInfluence : 0f);
            _compute.SetFloat(DisturbanceDampingId, _disturbanceDamping);
            _compute.SetVector(SwirlAngleId, _swirlAngle * Mathf.Deg2Rad);
            _compute.SetFloat(FlowInfluenceId, _flowInfluence);
            _compute.SetFloat(HeatGainId, _heatGain);
            _compute.SetFloat(HeatDecayId, _heatDecay);
            _compute.SetFloat(ColorLerpRateId, _colorLerpRate);
            _compute.SetTexture(_kernel, DisturbanceTexId, hasField ? _disturbance.FieldTexture : Texture2D.blackTexture);
            _compute.SetVector(FieldBoundsMinId, hasField ? _disturbance.FieldBoundsMin : Vector2.zero);
            _compute.SetVector(FieldBoundsSizeId, hasField ? _disturbance.FieldBoundsSize : Vector2.one);

            _compute.SetBuffer(_kernel, SpecksId, _speckBuffer);
            _compute.Dispatch(_kernel, Mathf.CeilToInt(_activeCount / (float)ThreadGroupSize), 1, 1);
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

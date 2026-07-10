using BalloonParty.Shared.Disturbance;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     A GPU-simulated field of ambient specks (dust/pollen). Each frame a compute pass advects every
    ///     speck by Brownian drift + the scenario's motion vector + the disturbance field, wrapping them
    ///     toroidally within a world region so the field is always populated; they render as billboards
    ///     straight from the GPU buffer (no CPU readback). The motion vector is the smoothed velocity of
    ///     <see cref="ScenarioContentRoot" />, so the field reacts to every scenario beat — the ascend, the
    ///     restart descent, the float-away — automatically.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    internal sealed class SpeckField : MonoBehaviour
    {
        private const int ThreadGroupSize = 64;
        private const int SpeckStrideBytes = sizeof(float) * 7;

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
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int FieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int RegionMinId = Shader.PropertyToID("_RegionMin");
        private static readonly int RegionSizeId = Shader.PropertyToID("_RegionSize");
        private static readonly int MinLifetimeId = Shader.PropertyToID("_MinLifetime");
        private static readonly int MaxLifetimeId = Shader.PropertyToID("_MaxLifetime");
        private static readonly int SpeckSizeId = Shader.PropertyToID("_SpeckSize");
        private static readonly int MinScaleId = Shader.PropertyToID("_MinScale");
        private static readonly int MaxScaleId = Shader.PropertyToID("_MaxScale");
        private static readonly int FadeInId = Shader.PropertyToID("_FadeIn");
        private static readonly int FadeOutId = Shader.PropertyToID("_FadeOut");

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

        [SerializeField] private float _speckSize = 0.03f;

        [Tooltip("Per-speck lifetime range (seconds). Each speck fades in, lives, fades out, then respawns.")]
        [SerializeField] private Vector2 _lifetimeRange = new(2f, 6f);

        [Tooltip("Per-speck scale multiplier range on _speckSize, for size variety.")]
        [SerializeField] private Vector2 _scaleRange = new(0.5f, 1.5f);

        [Tooltip("Fraction of life spent fading/scaling in.")]
        [Range(0f, 0.5f)] [SerializeField] private float _fadeIn = 0.15f;

        [Tooltip("Fraction of life spent fading/scaling out.")]
        [Range(0f, 0.5f)] [SerializeField] private float _fadeOut = 0.25f;

        [Tooltip("Per-frame root move (world units) above which it's treated as a teleport (e.g. the " +
                 "Ascent snapping the root to its start height) and ignored, not matched.")]
        [SerializeField] private float _teleportThreshold = 1f;

        [Inject] private ScenarioContentRoot _scenarioRoot;
        [Inject] private DisturbanceFieldService _disturbance;

        private ComputeBuffer _speckBuffer;
        private Mesh _mesh;
        private int _kernel;
        private Vector2 _lastRootPos;
        private Vector2 _motionDelta;
        private bool _motionSeeded;
        private bool _ready;

        private void Start()
        {
            if (_compute == null || _renderMaterial == null || _count <= 0)
            {
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

            _ready = true;
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            SampleMotionDelta();
            Dispatch(dt);
            PushRenderParams();
        }

        private void OnDestroy()
        {
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

        // Refresh the material state the command buffer draws with; runs before the camera renders.
        private void PushRenderParams()
        {
            _renderMaterial.SetBuffer(SpecksId, _speckBuffer);
            _renderMaterial.SetFloat(SpeckSizeId, _speckSize);
            _renderMaterial.SetFloat(MinScaleId, _scaleRange.x);
            _renderMaterial.SetFloat(MaxScaleId, _scaleRange.y);
            _renderMaterial.SetFloat(FadeInId, _fadeIn);
            _renderMaterial.SetFloat(FadeOutId, _fadeOut);
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
            _compute.SetInt(CountId, _count);
            _compute.SetFloat(DeltaTimeId, dt);
            _compute.SetFloat(TimeId, Time.unscaledTime);
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
            _compute.SetTexture(_kernel, DisturbanceTexId, hasField ? _disturbance.FieldTexture : Texture2D.blackTexture);
            _compute.SetVector(FieldBoundsMinId, hasField ? _disturbance.FieldBoundsMin : Vector2.zero);
            _compute.SetVector(FieldBoundsSizeId, hasField ? _disturbance.FieldBoundsSize : Vector2.one);

            _compute.SetBuffer(_kernel, SpecksId, _speckBuffer);
            _compute.Dispatch(_kernel, Mathf.CeilToInt(_count / (float)ThreadGroupSize), 1, 1);
        }

        private struct Speck
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Seed;
            public float Age;
            public float Lifetime;
        }
    }
}

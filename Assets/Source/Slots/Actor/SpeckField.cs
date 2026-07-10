using BalloonParty.Shared.Disturbance;
using UnityEngine;
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
    internal sealed class SpeckField : MonoBehaviour
    {
        private const int ThreadGroupSize = 64;
        private const int SpeckStrideBytes = sizeof(float) * 5;

        private static readonly int SpecksId = Shader.PropertyToID("_Specks");
        private static readonly int CountId = Shader.PropertyToID("_Count");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimeId = Shader.PropertyToID("_TimeSeconds");
        private static readonly int MotionVectorId = Shader.PropertyToID("_MotionVector");
        private static readonly int BrownianStrengthId = Shader.PropertyToID("_BrownianStrength");
        private static readonly int DragId = Shader.PropertyToID("_Drag");
        private static readonly int MotionInfluenceId = Shader.PropertyToID("_MotionInfluence");
        private static readonly int DisturbanceInfluenceId = Shader.PropertyToID("_DisturbanceInfluence");
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int FieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int RegionMinId = Shader.PropertyToID("_RegionMin");
        private static readonly int RegionSizeId = Shader.PropertyToID("_RegionSize");
        private static readonly int SpeckSizeId = Shader.PropertyToID("_SpeckSize");

        [SerializeField] private ComputeShader _compute;
        [SerializeField] private Material _renderMaterial;
        [SerializeField] private int _count = 4096;
        [SerializeField] private Vector2 _regionSize = new(30f, 20f);
        [SerializeField] private float _brownianStrength = 0.6f;
        [SerializeField] private float _drag = 2f;
        [SerializeField] private float _motionInfluence = 1f;
        [SerializeField] private float _disturbanceInfluence = 1f;
        [SerializeField] private float _speckSize = 0.03f;

        [Tooltip("Root speed (world units/sec) mapped to a motion vector of magnitude 1.")]
        [SerializeField] private float _referenceSpeed = 20f;

        [Tooltip("Response rate for the motion vector — higher snaps to velocity faster.")]
        [SerializeField] private float _motionSmoothing = 12f;

        [Inject] private ScenarioContentRoot _scenarioRoot;
        [Inject] private DisturbanceFieldService _disturbance;

        private ComputeBuffer _speckBuffer;
        private int _kernel;
        private Vector2 _lastRootPos;
        private Vector2 _motionVector;
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

            UpdateMotionVector(dt);
            Dispatch(dt);
        }

        private void OnRenderObject()
        {
            if (!_ready)
            {
                return;
            }

            _renderMaterial.SetBuffer(SpecksId, _speckBuffer);
            _renderMaterial.SetFloat(SpeckSizeId, _speckSize);
            _renderMaterial.SetPass(0);

            // 6 verts (a quad) per speck; the vertex shader expands from SV_VertexID + SV_InstanceID.
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, _count);
        }

        private void OnDestroy()
        {
            _speckBuffer?.Release();
            _speckBuffer = null;
        }

        private void SeedSpecks()
        {
            var specks = new Speck[_count];
            var min = _regionSize * -0.5f;
            for (var i = 0; i < _count; i++)
            {
                specks[i] = new Speck
                {
                    Position = new Vector2(
                        min.x + Random.value * _regionSize.x,
                        min.y + Random.value * _regionSize.y),
                    Velocity = Vector2.zero,
                    Seed = Random.value,
                };
            }

            _speckBuffer.SetData(specks);
        }

        private void UpdateMotionVector(float dt)
        {
            // Frame-rate-independent exponential smoothing so the field eases in/out of a scenario move.
            var smoothing = 1f - Mathf.Exp(-_motionSmoothing * dt);

            // Injection can lag this component's Start; until the root resolves, ease the field to rest so
            // it still drifts on Brownian alone rather than throwing.
            if (_scenarioRoot?.Transform == null)
            {
                _motionSeeded = false;
                _motionVector = Vector2.Lerp(_motionVector, Vector2.zero, smoothing);
                return;
            }

            var pos = RootPosition();
            if (!_motionSeeded)
            {
                _lastRootPos = pos;
                _motionSeeded = true;
            }

            var raw = _referenceSpeed > 0f ? (pos - _lastRootPos) / dt / _referenceSpeed : Vector2.zero;
            _lastRootPos = pos;
            _motionVector = Vector2.Lerp(_motionVector, raw, smoothing);
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
            _compute.SetVector(MotionVectorId, _motionVector);
            _compute.SetFloat(BrownianStrengthId, _brownianStrength);
            _compute.SetFloat(DragId, _drag);
            _compute.SetFloat(MotionInfluenceId, _motionInfluence);
            _compute.SetVector(RegionMinId, _regionSize * -0.5f);
            _compute.SetVector(RegionSizeId, _regionSize);

            var hasField = _disturbance != null && _disturbance.FieldTexture != null;
            _compute.SetFloat(DisturbanceInfluenceId, hasField ? _disturbanceInfluence : 0f);
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
        }
    }
}

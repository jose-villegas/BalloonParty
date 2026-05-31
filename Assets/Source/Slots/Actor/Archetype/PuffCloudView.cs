#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Drives the <c>BalloonParty/Grid/PuffCloud</c> shader on a
    /// <see cref="SpriteRenderer"/> quad with no assigned sprite.
    /// Pushes <c>_TimeOffset</c>, slot-center data, and the density
    /// <see cref="RenderTexture"/> via <see cref="MaterialPropertyBlock"/>
    /// each frame.
    ///
    /// <c>[ExecuteAlways]</c> keeps the cloud animation running in edit mode.
    /// P1/P2 uses <c>[SerializeField]</c> fields for visual tuning; these will
    /// migrate to a <c>PuffCloudSettings</c> ScriptableObject in P3.
    /// </summary>
    [ExecuteAlways]
    internal class PuffCloudView : MonoBehaviour
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int DensityTexId = Shader.PropertyToID("_DensityTex");
        private static readonly int DiffusionRateId = Shader.PropertyToID("_DiffusionRate");
        private static readonly int ReformSpeedId = Shader.PropertyToID("_ReformSpeed");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int StampCenterId = Shader.PropertyToID("_StampCenter");
        private static readonly int StampRadiusId = Shader.PropertyToID("_StampRadius");
        private static readonly int StampStrengthId = Shader.PropertyToID("_StampStrength");
        private static readonly int StampDirectionId = Shader.PropertyToID("_StampDirection");

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Density Field")]
        [Tooltip("Resolution per slot axis. Single-slot cloud = texelsPerSlot x texelsPerSlot.")]
        [SerializeField] private int _texelsPerSlot = 32;

        [Tooltip("Spatial blur rate per diffusion tick.")]
        [SerializeField] [Range(0f, 1f)] private float _diffusionRate = 0.15f;

        [Tooltip("Speed at which density trends back toward 1.0 (equilibrium).")]
        [SerializeField] [Range(0f, 2f)] private float _reformSpeed = 0.4f;

        [Tooltip("Seconds between diffusion blit passes. Lower = smoother but more GPU work.")]
        [SerializeField] [Range(0.016f, 0.2f)] private float _diffusionTickInterval = 0.05f;

        [Header("Debug")]
        [Tooltip("When enabled, clicking on the cloud stamps a test disturbance.")]
        [SerializeField] private bool _debugClickToStamp;

        [SerializeField] [Range(0.05f, 0.5f)] private float _debugStampRadius = 0.15f;
        [SerializeField] [Range(0.1f, 1.5f)] private float _debugStampStrength = 0.8f;

        private readonly Vector4[] _slotCenters = new Vector4[16];

        private MaterialPropertyBlock _block;
        private float _instancePhase;
        private RenderTexture _densityA;
        private RenderTexture _densityB;
        private bool _readFromA = true;
        private Material _diffusionMaterial;
        private Material _stampMaterial;
        private float _diffusionTimer;
        private bool _densityInitialized;

        private RenderTexture DensityRead => _readFromA ? _densityA : _densityB;
        private RenderTexture DensityWrite => _readFromA ? _densityB : _densityA;

        private void Awake()
        {
            EnsureBlock();
            _instancePhase = Random.value * 100f;
        }

        private void OnEnable()
        {
            InitDensityField();
            PushSlotCenters();
        }

        private void OnDisable()
        {
            ReleaseDensityField();
        }

        private void Update()
        {
            EnsureBlock();

            var currentTime = Time.time;
            var dt = Time.deltaTime;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentTime = (float)EditorApplication.timeSinceStartup;
                dt = 0.016f;
                SceneView.RepaintAll();
            }
#endif

            PushTime(currentTime);
            TickDiffusion(dt);
            PushDensityTexture();

#if UNITY_EDITOR
            HandleDebugClick();
#else
            if (_debugClickToStamp)
            {
                HandleDebugClick();
            }
#endif
        }

        private void OnValidate()
        {
            EnsureBlock();
            PushSlotCenters();
        }

        private void OnDestroy()
        {
            DestroyMaterial(ref _diffusionMaterial);
            DestroyMaterial(ref _stampMaterial);
        }

        /// <summary>
        /// Stamps a disturbance onto the density field at the given world
        /// position. The cloud will part at this location and reform over
        /// time via the diffusion pass.
        /// </summary>
        internal void StampDisturbance(Vector3 worldPosition, float radius, float strength, Vector2 direction)
        {
            if (!_densityInitialized)
            {
                return;
            }

            EnsureStampMaterial();

            var localPos = WorldToDensityUV(worldPosition);
            var radiusUV = WorldRadiusToDensityUV(radius);

            _stampMaterial.SetVector(StampCenterId, new Vector4(localPos.x, localPos.y, 0f, 0f));
            _stampMaterial.SetFloat(StampRadiusId, radiusUV);
            _stampMaterial.SetFloat(StampStrengthId, strength);
            _stampMaterial.SetVector(StampDirectionId, new Vector4(direction.x, direction.y, 0f, 0f));

            Graphics.Blit(DensityRead, DensityWrite, _stampMaterial);
            _readFromA = !_readFromA;
        }

        private void EnsureBlock()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
        }

        private void InitDensityField()
        {
            if (_densityInitialized)
            {
                return;
            }

            var res = Mathf.Max(4, _texelsPerSlot);
            _densityA = CreateDensityRT(res, res);
            _densityB = CreateDensityRT(res, res);

            ClearToWhite(_densityA);
            ClearToWhite(_densityB);

            _readFromA = true;
            _densityInitialized = true;
            _diffusionTimer = 0f;
        }

        private void ReleaseDensityField()
        {
            ReleaseRT(ref _densityA);
            ReleaseRT(ref _densityB);
            _densityInitialized = false;
        }

        private static RenderTexture CreateDensityRT(int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.R8)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        private static void ClearToWhite(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.white);
            RenderTexture.active = prev;
        }

        private static void ReleaseRT(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
                rt = null;
            }
        }

        private void EnsureDiffusionMaterial()
        {
            if (_diffusionMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/BalloonParty/Grid/PuffCloudDiffusion");
            if (shader == null)
            {
                Debug.LogError("PuffCloudView: PuffCloudDiffusion shader not found.", this);
                return;
            }

            _diffusionMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void EnsureStampMaterial()
        {
            if (_stampMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/BalloonParty/Grid/PuffCloudStamp");
            if (shader == null)
            {
                Debug.LogError("PuffCloudView: PuffCloudStamp shader not found.", this);
                return;
            }

            _stampMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void TickDiffusion(float dt)
        {
            if (!_densityInitialized)
            {
                return;
            }

            _diffusionTimer += dt;
            if (_diffusionTimer < _diffusionTickInterval)
            {
                return;
            }

            EnsureDiffusionMaterial();
            if (_diffusionMaterial == null)
            {
                return;
            }

            _diffusionMaterial.SetFloat(DiffusionRateId, _diffusionRate);
            _diffusionMaterial.SetFloat(ReformSpeedId, _reformSpeed);
            _diffusionMaterial.SetFloat(DeltaTimeId, _diffusionTimer);

            Graphics.Blit(DensityRead, DensityWrite, _diffusionMaterial);
            _readFromA = !_readFromA;
            _diffusionTimer = 0f;
        }

        private void PushDensityTexture()
        {
            if (_renderer == null || _block == null || !_densityInitialized)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetTexture(DensityTexId, DensityRead);
            _renderer.SetPropertyBlock(_block);
        }

        private void PushTime(float currentTime)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(TimeOffsetId, (currentTime * _animationSpeed) + _instancePhase);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Pushes slot center positions to the shader. In P1/P2 there is a single
        /// slot — the world position of this transform. P3 will pass multiple
        /// centers for merged clusters.
        /// </summary>
        private void PushSlotCenters()
        {
            EnsureBlock();

            if (_renderer == null || _block == null)
            {
                return;
            }

            var pos = transform.position;
            _slotCenters[0] = new Vector4(pos.x, pos.y, 0f, 0f);

            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
            _block.SetInt(SlotCountId, 1);
            _renderer.SetPropertyBlock(_block);
        }

        private Vector2 WorldToDensityUV(Vector3 worldPos)
        {
            if (_renderer == null)
            {
                return new Vector2(0.5f, 0.5f);
            }

            var bounds = _renderer.bounds;
            return new Vector2(
                (worldPos.x - bounds.min.x) / bounds.size.x,
                (worldPos.y - bounds.min.y) / bounds.size.y);
        }

        private float WorldRadiusToDensityUV(float worldRadius)
        {
            if (_renderer == null)
            {
                return 0.1f;
            }

            var bounds = _renderer.bounds;
            var avgSize = (bounds.size.x + bounds.size.y) * 0.5f;
            return avgSize > 0.001f ? worldRadius / avgSize : 0.1f;
        }

        private void HandleDebugClick()
        {
            if (!_debugClickToStamp)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            if (_renderer == null || !_renderer.bounds.Contains(mouseWorld))
            {
                return;
            }

            StampDisturbance(mouseWorld, _debugStampRadius, _debugStampStrength, Vector2.zero);
        }

        private static void DestroyMaterial(ref Material mat)
        {
            if (mat != null)
            {
                DestroyImmediate(mat);
                mat = null;
            }
        }
    }
}

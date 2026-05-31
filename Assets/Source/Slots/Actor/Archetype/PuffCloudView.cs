#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
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
    /// Supports both standalone mode (serialized fields) and configured mode
    /// (driven by <see cref="PuffCloudViewController"/> with a
    /// <see cref="PuffCloudSettings"/> SO).
    /// </summary>
    [ExecuteAlways]
    internal class PuffCloudView : MonoBehaviour, IPoolable
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int DensityTexId = Shader.PropertyToID("_DensityTex");
        private static readonly int DiffusionRateId = Shader.PropertyToID("_DiffusionRate");
        private static readonly int ReformSpeedId = Shader.PropertyToID("_ReformSpeed");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int WindDirId = Shader.PropertyToID("_WindDir");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int PressureStrId = Shader.PropertyToID("_PressureStr");
        private static readonly int StampCenterId = Shader.PropertyToID("_StampCenter");
        private static readonly int StampRadiusId = Shader.PropertyToID("_StampRadius");
        private static readonly int StampStrengthId = Shader.PropertyToID("_StampStrength");
        private static readonly int StampDirectionId = Shader.PropertyToID("_StampDirection");
        private static readonly int DisplaceAmountId = Shader.PropertyToID("_DisplaceAmount");
        private static readonly int DisplaceDecayId = Shader.PropertyToID("_DisplaceDecay");

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Density Field")]
        [Tooltip("Resolution per slot axis. Single-slot cloud = texelsPerSlot x texelsPerSlot.")]
        [SerializeField] private int _texelsPerSlot = 32;

        [Tooltip("Spatial blur rate per diffusion tick. Higher = faster spread from neighbors.")]
        [SerializeField] [Range(0f, 1f)] private float _diffusionRate = 0.3f;

        [Tooltip("Speed at which density trends back toward 1.0 (equilibrium).")]
        [SerializeField] [Range(0f, 0.5f)] private float _reformSpeed = 0.05f;

        [Tooltip("Seconds between diffusion blit passes.")]
        [SerializeField] [Range(0.016f, 0.2f)] private float _diffusionTickInterval = 0.05f;

        [Header("Wind")]
        [SerializeField] [Range(0f, 5f)] private float _windSpeed = 1.0f;
        [SerializeField] [Range(0.5f, 20f)] private float _windSmoothing = 6.0f;
        [SerializeField] [Range(0.5f, 10f)] private float _windDecay = 2.0f;
        [SerializeField] [Range(0f, 1f)] private float _pressureStrength = 0.4f;

        [Header("Displacement")]
        [SerializeField] [Range(0f, 1f)] private float _displaceAmount = 0.3f;
        [SerializeField] [Range(0f, 5f)] private float _displaceDecay = 1.5f;

        [Header("Debug")]
        [Tooltip("When enabled, clicking on the cloud stamps a test disturbance.")]
        [SerializeField] private bool _debugClickToStamp;

        [SerializeField] [Range(0.01f, 0.3f)] private float _debugStampRadius = 0.05f;
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
        private Vector3 _debugLastMouseWorld;
        private Vector2 _windTarget;
        private Vector2 _windCurrent;

        private bool _configured;
        private int _slotCount;
        private Rect _worldBounds;
        private int _densityWidth;
        private int _densityHeight;

        private RenderTexture DensityRead => _readFromA ? _densityA : _densityB;
        private RenderTexture DensityWrite => _readFromA ? _densityB : _densityA;

        internal SpriteRenderer Renderer => _renderer;

        private void Awake()
        {
            EnsureBlock();
            _instancePhase = Random.value * 100f;
        }

        private void OnEnable()
        {
            if (!_configured)
            {
                InitDensityField(_texelsPerSlot, _texelsPerSlot);
                PushSlotCentersDefault();
            }
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
            if (!_configured)
            {
                PushSlotCentersDefault();
            }
        }

        private void OnDestroy()
        {
            DestroyMaterial(ref _diffusionMaterial);
            DestroyMaterial(ref _stampMaterial);
        }

        public void OnSpawned()
        {
            _instancePhase = Random.value * 100f;
            _windCurrent = Vector2.zero;
            _windTarget = Vector2.zero;
        }

        public void OnDespawned()
        {
            _configured = false;
            _slotCount = 0;
            ReleaseDensityField();
        }

        /// <summary>
        /// Configures the cloud view to render a cluster spanning the given
        /// slot world positions and bounding box. Called by
        /// <see cref="PuffCloudViewController"/> when a cluster is created or resized.
        /// </summary>
        internal void Configure(Vector3[] slotWorldPositions, Rect worldBounds, PuffCloudSettings settings)
        {
            _configured = true;

            ApplySettings(settings);

            var oldBounds = _worldBounds;
            _worldBounds = worldBounds;
            _slotCount = Mathf.Min(slotWorldPositions.Length, _slotCenters.Length);

            for (var i = 0; i < _slotCount; i++)
            {
                var pos = slotWorldPositions[i];
                _slotCenters[i] = new Vector4(pos.x, pos.y, 0f, 0f);
            }

            // Position and scale the quad to cover world bounds + padding
            var padding = settings.Padding;
            var center = worldBounds.center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);

            var scaleX = worldBounds.width + padding * 2f;
            var scaleY = worldBounds.height + padding * 2f;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            // Compute density RT resolution based on bounds
            var slotsWide = Mathf.Max(1, Mathf.CeilToInt(worldBounds.width));
            var slotsTall = Mathf.Max(1, Mathf.CeilToInt(worldBounds.height));
            var newWidth = slotsWide * _texelsPerSlot;
            var newHeight = slotsTall * _texelsPerSlot;

            if (_densityInitialized && settings.PreserveDensityOnResize
                                    && _densityWidth == newWidth && _densityHeight == newHeight)
            {
                // Same size — no resize needed
            }
            else if (_densityInitialized && settings.PreserveDensityOnResize
                                         && (_densityWidth != newWidth || _densityHeight != newHeight))
            {
                ResizeDensityField(newWidth, newHeight, oldBounds, worldBounds);
            }
            else
            {
                ReleaseDensityField();
                InitDensityField(newWidth, newHeight);
            }

            PushSlotCentersConfigured();
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
            _stampMaterial.SetFloat(DisplaceAmountId, _displaceAmount);

            if (direction.sqrMagnitude > 0.001f)
            {
                _windTarget = -direction;
            }

            Graphics.Blit(DensityRead, DensityWrite, _stampMaterial);
            _readFromA = !_readFromA;
        }

        /// <summary>
        /// Returns the cluster world bounds used for density UV mapping.
        /// Falls back to renderer bounds in unconfigured (standalone) mode.
        /// </summary>
        internal Rect GetWorldBounds()
        {
            if (_configured)
            {
                return _worldBounds;
            }

            if (_renderer != null)
            {
                var b = _renderer.bounds;
                return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }

            return new Rect(transform.position.x - 0.5f, transform.position.y - 0.5f, 1f, 1f);
        }

        private void ApplySettings(PuffCloudSettings settings)
        {
            _animationSpeed = settings.AnimationSpeed;
            _texelsPerSlot = settings.TexelsPerSlot;
            _diffusionRate = settings.DiffusionRate;
            _reformSpeed = settings.ReformSpeed;
            _diffusionTickInterval = settings.DiffusionTickInterval;
            _windSpeed = settings.WindSpeed;
            _windSmoothing = settings.WindSmoothing;
            _windDecay = settings.WindDecay;
            _pressureStrength = settings.PressureStrength;
            _displaceAmount = settings.DisplaceAmount;
            _displaceDecay = settings.DisplaceDecay;
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

        private void InitDensityField(int width, int height)
        {
            if (_densityInitialized)
            {
                return;
            }

            _densityWidth = Mathf.Max(4, width);
            _densityHeight = Mathf.Max(4, height);
            _densityA = CreateDensityRT(_densityWidth, _densityHeight);
            _densityB = CreateDensityRT(_densityWidth, _densityHeight);

            ClearToEquilibrium(_densityA);
            ClearToEquilibrium(_densityB);

            _readFromA = true;
            _densityInitialized = true;
            _diffusionTimer = 0f;
        }

        private void ResizeDensityField(int newWidth, int newHeight, Rect oldBounds, Rect newBounds)
        {
            var newA = CreateDensityRT(newWidth, newHeight);
            var newB = CreateDensityRT(newWidth, newHeight);

            ClearToEquilibrium(newA);
            ClearToEquilibrium(newB);

            // Blit old content into new RT at the correct UV sub-rect
            if (_densityA != null && oldBounds.width > 0.001f && oldBounds.height > 0.001f)
            {
                var offsetX = (oldBounds.xMin - newBounds.xMin) / newBounds.width;
                var offsetY = (oldBounds.yMin - newBounds.yMin) / newBounds.height;
                var scaleX = oldBounds.width / newBounds.width;
                var scaleY = oldBounds.height / newBounds.height;

                var scale = new Vector2(scaleX, scaleY);
                var offset = new Vector2(offsetX, offsetY);

                Graphics.Blit(DensityRead, newA, scale, offset);
            }

            ReleaseRT(ref _densityA);
            ReleaseRT(ref _densityB);

            _densityA = newA;
            _densityB = newB;
            _readFromA = true;
            _densityWidth = newWidth;
            _densityHeight = newHeight;
        }

        private void ReleaseDensityField()
        {
            ReleaseRT(ref _densityA);
            ReleaseRT(ref _densityB);
            _densityInitialized = false;
        }

        private static RenderTexture CreateDensityRT(int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        private static void ClearToEquilibrium(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, new Color(1f, 0.5f, 0.5f, 1f));
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

            _windCurrent = Vector2.Lerp(_windCurrent, _windTarget, _windSmoothing * _diffusionTimer);
            _windTarget = Vector2.Lerp(_windTarget, Vector2.zero, _windDecay * _diffusionTimer);

            _diffusionMaterial.SetVector(WindDirId, new Vector4(_windCurrent.x, _windCurrent.y, 0f, 0f));
            _diffusionMaterial.SetFloat(WindSpeedId, _windSpeed);
            _diffusionMaterial.SetFloat(PressureStrId, _pressureStrength);
            _diffusionMaterial.SetFloat(DisplaceDecayId, _displaceDecay);

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

        private void PushSlotCentersDefault()
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

        private void PushSlotCentersConfigured()
        {
            EnsureBlock();

            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
            _block.SetInt(SlotCountId, _slotCount);
            _renderer.SetPropertyBlock(_block);
        }

        private Vector2 WorldToDensityUV(Vector3 worldPos)
        {
            if (_configured)
            {
                return new Vector2(
                    (worldPos.x - _worldBounds.xMin) / _worldBounds.width,
                    (worldPos.y - _worldBounds.yMin) / _worldBounds.height);
            }

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
            if (_configured)
            {
                var avgSize = (_worldBounds.width + _worldBounds.height) * 0.5f;
                return avgSize > 0.001f ? worldRadius / avgSize : 0.1f;
            }

            if (_renderer == null)
            {
                return 0.1f;
            }

            var bounds = _renderer.bounds;
            var avg = (bounds.size.x + bounds.size.y) * 0.5f;
            return avg > 0.001f ? worldRadius / avg : 0.1f;
        }

        private void HandleDebugClick()
        {
            if (!_debugClickToStamp)
            {
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                _debugLastMouseWorld = Vector3.zero;
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
                _debugLastMouseWorld = mouseWorld;
                return;
            }

            var direction = Vector2.zero;
            if (_debugLastMouseWorld != Vector3.zero)
            {
                var delta = mouseWorld - _debugLastMouseWorld;
                direction = new Vector2(delta.x, delta.y);
            }

            _debugLastMouseWorld = mouseWorld;

            StampDisturbance(mouseWorld, _debugStampRadius, _debugStampStrength, direction.normalized);
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

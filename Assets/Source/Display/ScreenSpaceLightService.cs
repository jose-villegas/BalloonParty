using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Screen-space light approximation ("2D GI" — see PLAN-ScreenSpaceLight.md,
    ///     prototype): smears the <see cref="SceneCaptureService"/> capture toward a global
    ///     light direction into a tiny light buffer (shadow amount in A, bounce color in
    ///     RGB), then composites it over the whole frame as a camera-fitted quad with a
    ///     multiplicative blend — no material changes anywhere else, no post-processing
    ///     readback. The overlay quad lives on the TransparentFX layer, which must stay
    ///     excluded from the capture mask so the capture always sees the unlit scene (no
    ///     frame-to-frame feedback). Disable the component to A/B the effect; knobs are
    ///     serialized here while this is an experiment and graduate to a config asset if
    ///     the effect stays.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(SceneCaptureService))]
    public class ScreenSpaceLightService : MonoBehaviour
    {
        private const int TapCount = 8;
        private const float OverlayDistance = 5f;

        private static readonly int TapStepUvId = Shader.PropertyToID("_TapStepUV");
        private static readonly int TapDecayId = Shader.PropertyToID("_TapDecay");
        private static readonly int TapStartId = Shader.PropertyToID("_TapStart");
        private static readonly int HistoryTexId = Shader.PropertyToID("_HistoryTex");
        private static readonly int TemporalBlendId = Shader.PropertyToID("_TemporalBlend");
        private static readonly int LightTexId = Shader.PropertyToID("_LightTex");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int BounceStrengthId = Shader.PropertyToID("_BounceStrength");
        private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");

        [Tooltip("Assign explicitly — device builds strip shaders that are only " +
                 "referenced via Shader.Find, which is kept as an editor fallback.")]
        [SerializeField] private Shader _smearShader;
        [SerializeField] private Shader _overlayShader;
        [Tooltip("Light vector — points TOWARD the light source, same convention (and " +
                 "default) as the PuffCloud material's Light Direction. Shadows extend " +
                 "the opposite way.")]
        [SerializeField] private Vector2 _lightDirection = new Vector2(1f, -1f);
        [Tooltip("How far an object's shadow/bleed reaches, in world units.")]
        [SerializeField] private float _smearDistance = 1.5f;
        [Tooltip("Per-tap weight decay along the march — lower dies off faster.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _tapDecay = 0.8f;
        [Tooltip("Taps skipped at the march start so occluders don't fully self-shadow.")]
        [SerializeField] private float _tapStart = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float _shadowStrength = 0.35f;
        [SerializeField] private Color _shadowTint = new Color(0.55f, 0.6f, 0.75f);
        [Range(0f, 2f)]
        [SerializeField] private float _bounceStrength = 0.25f;
        [Tooltip("Fraction of the fresh light buffer accepted per frame — lower is " +
                 "smoother but laggier. Kills the texel flicker of moving sprites at " +
                 "capture resolution.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float _temporalResponse = 0.2f;
        [Tooltip("Overlay sorting — above all gameplay, below UI.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Sky";
        [SerializeField] private int _sortingOrder = 32000;

        private static int _overlayLayer = -1;

        private Camera _camera;
        private SceneCaptureService _capture;
        private Material _smearMaterial;
        private Material _overlayMaterial;
        private RenderTexture _smearTarget;
        private RenderTexture _workTarget;
        private RenderTexture _lightTarget;
        private RenderTexture _historyTarget;
        private MeshRenderer _overlayRenderer;
        private Transform _overlayTransform;
        private bool _historyValid;

        // What the overlay currently displays — the ping-pong swap means neither field
        // consistently holds the latest build.
        internal RenderTexture LightTexture { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _capture = GetComponent<SceneCaptureService>();
        }

        private void OnEnable()
        {
            _capture.Acquire();
        }

        private void OnPreRender()
        {
            // Lower-depth cameras (the capture) have already rendered this frame, so the
            // smear reads a current capture, not last frame's.
            var source = _capture.CaptureTexture;
            if (source == null)
            {
                SetOverlayVisible(false);
                return;
            }

            if (!EnsureResources())
            {
                enabled = false;
                return;
            }

            EnsureTargets(source);
            PushParameters();

            Graphics.Blit(source, _smearTarget, _smearMaterial, 0);
            Graphics.Blit(_smearTarget, _workTarget, _smearMaterial, 1);

            // Temporal accumulation: fold the fresh build into the previous smoothed
            // buffer, then swap so this frame's output is next frame's history.
            _smearMaterial.SetTexture(HistoryTexId, _historyTarget);
            _smearMaterial.SetFloat(TemporalBlendId, _historyValid ? _temporalResponse : 1f);
            Graphics.Blit(_workTarget, _lightTarget, _smearMaterial, 2);
            _historyValid = true;

            _overlayMaterial.SetTexture(LightTexId, _lightTarget);
            LightTexture = _lightTarget;
            (_historyTarget, _lightTarget) = (_lightTarget, _historyTarget);

            FitOverlayToFrustum();
            SetOverlayVisible(true);
        }

        private void OnDisable()
        {
            _capture.Release();
            SetOverlayVisible(false);
        }

        private void OnDestroy()
        {
            ReleaseTarget(ref _smearTarget);
            ReleaseTarget(ref _workTarget);
            ReleaseTarget(ref _lightTarget);
            ReleaseTarget(ref _historyTarget);

            if (_smearMaterial != null)
            {
                Destroy(_smearMaterial);
            }

            if (_overlayMaterial != null)
            {
                Destroy(_overlayMaterial);
            }
        }

        private bool EnsureResources()
        {
            if (_overlayRenderer != null)
            {
                return true;
            }

            // Editor-only convenience: on device, unassigned means unavailable — builds
            // strip shaders that are only referenced by name.
            if (_smearShader == null)
            {
                _smearShader = Shader.Find("Hidden/BalloonParty/Display/ScreenSpaceLightSmear");
            }

            if (_overlayShader == null)
            {
                _overlayShader = Shader.Find("BalloonParty/Display/ScreenSpaceLightOverlay");
            }

            if (_smearShader == null || _overlayShader == null)
            {
                Debug.LogWarning("ScreenSpaceLightService: shader references missing — assign " +
                                 "them on the component; Shader.Find-only shaders are stripped " +
                                 "from builds. Disabling.", this);
                return false;
            }

            _smearMaterial = new Material(_smearShader);
            _overlayMaterial = new Material(_overlayShader);

            if (_overlayLayer < 0)
            {
                _overlayLayer = LayerMask.NameToLayer("TransparentFX");
            }

            var overlay = new GameObject("ScreenSpaceLightOverlay")
            {
                layer = _overlayLayer
            };
            _overlayTransform = overlay.transform;
            _overlayTransform.SetParent(transform, false);
            _overlayTransform.localPosition = new Vector3(0f, 0f, OverlayDistance);

            var filter = overlay.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshHelper.CreateQuad(QuadPivot.Center);

            _overlayRenderer = overlay.AddComponent<MeshRenderer>();
            _overlayRenderer.sharedMaterial = _overlayMaterial;
            _overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.sortingLayerName = _sortingLayerName;
            _overlayRenderer.sortingOrder = _sortingOrder;

            return true;
        }

        private void EnsureTargets(RenderTexture source)
        {
            if (_lightTarget != null && _lightTarget.width == source.width
                                     && _lightTarget.height == source.height)
            {
                return;
            }

            ReleaseTarget(ref _smearTarget);
            ReleaseTarget(ref _workTarget);
            ReleaseTarget(ref _lightTarget);
            ReleaseTarget(ref _historyTarget);

            _smearTarget = CreateTarget(source, "ScreenSpaceLightSmear");
            _workTarget = CreateTarget(source, "ScreenSpaceLightWork");
            _lightTarget = CreateTarget(source, "ScreenSpaceLightA");
            _historyTarget = CreateTarget(source, "ScreenSpaceLightB");
            _historyValid = false;
        }

        // Pushed every frame on purpose: the knobs stay live-tunable in play mode and the
        // buffers are tiny. If the effect graduates, sync this (and the blits) to the
        // capture's Nth-frame cadence instead.
        private void PushParameters()
        {
            var worldHeight = _camera.orthographicSize * 2f;
            var worldWidth = worldHeight * _camera.aspect;

            var direction = _lightDirection.sqrMagnitude > 0.0001f
                ? _lightDirection.normalized
                : Vector2.down;

            // _lightDirection is an L vector, pointing AT the source. The shader marches
            // this step in both directions per pixel: +step for reflection (a lit
            // neighbour toward the source bleeds its color here) and -step for shadow
            // (an occluder toward the source darkens here) — see the shader header for
            // why the two need opposite marches.
            var stepWorld = _smearDistance / TapCount;
            var stepUv = new Vector4(
                direction.x * stepWorld / worldWidth,
                direction.y * stepWorld / worldHeight);

            _smearMaterial.SetVector(TapStepUvId, stepUv);
            _smearMaterial.SetFloat(TapDecayId, _tapDecay);
            _smearMaterial.SetFloat(TapStartId, _tapStart);

            _overlayMaterial.SetColor(ShadowTintId, _shadowTint);
            _overlayMaterial.SetFloat(ShadowStrengthId, _shadowStrength);
            _overlayMaterial.SetFloat(BounceStrengthId, _bounceStrength);

            // The bounce is measured against the sky the capture clears to (the main
            // camera's background), so open sky nets to neutral instead of tinting.
            _overlayMaterial.SetColor(AmbientColorId, _camera.backgroundColor);
        }

        private void FitOverlayToFrustum()
        {
            var height = _camera.orthographicSize * 2f;
            _overlayTransform.localScale = new Vector3(height * _camera.aspect, height, 1f);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayRenderer != null && _overlayRenderer.enabled != visible)
            {
                _overlayRenderer.enabled = visible;
            }
        }

        private static RenderTexture CreateTarget(RenderTexture source, string name)
        {
            return new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void ReleaseTarget(ref RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            target.Release();
            Destroy(target);
            target = null;
        }
    }
}

using System.Collections.Generic;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     An effects map (@ref plan_effects_layer_clearing) for the camera it sits on. While the clearing mask is
    ///     open, the camera stops drawing its effect layers; an effects camera renders them into the map instead,
    ///     and a camera-fitted quad lays the map back over the frame, faded where the mask is clear. With the mask
    ///     empty that is the same blend into the same frame, so it only runs while a clearing is open, and for
    ///     <see cref="IEffectsClearingSettings.MapHoldSeconds" /> after: otherwise the effects draw straight into
    ///     the frame, as they would without it. Within that, the effects camera renders only while something is
    ///     alive on its layers (<see cref="LayerPresence" />): an empty map would lay back nothing. The quad stays
    ///     while the glass is up, for its glint. The HUD camera has one for the score trails; the main camera one
    ///     for the chaos a ball flies through, in HDR so bloom and the tonemapper still see it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(EffectsClearingMaskView.ExecutionOrder + 1)]
    internal class EffectsMapCompositor : MonoBehaviour
    {
        private const float QuadDistance = 5f;
#if UNITY_ENABLE_CHECKS
        private const float UntrackedScanSeconds = 1f;
#endif

        private static readonly int MapTexId = Shader.PropertyToID("_EffectsMapTex");

        [Tooltip("The effects this camera draws, moved into the map while a clearing is open.")]
        [SerializeField] private LayerMask _effectLayers;

        [Tooltip("The base camera of this camera's stack. The effects camera follows its projection and renders " +
                 "just before it, so the map is ready the same frame.")]
        [SerializeField] private Camera _stackBase;

        [Tooltip("An HDR map (ARGBHalf) for a camera that post-processes, so bloom still sees values over 1; an " +
                 "8-bit map for one drawn after post. Its composite runs before post, so the effects are graded too.")]
        [SerializeField] private bool _hdr;

        [Header("Composite")]
        [Tooltip("Physics layer of the composite quad: one this camera draws and the effects camera doesn't.")]
        [SerializeField] private string _compositeLayerName = "UI";

        [Tooltip("Where the map lands among this camera's own renderers. Inside the map the effects keep their own " +
                 "order; this is where they all sit against the rest. The HUD's sits with the score trails (UI 3300), " +
                 "over the popups and under the card reveal.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "UI";

        [SerializeField] private int _sortingOrder = 3300;

        [Inject] private IEffectsClearingSettings _settings;
        [Inject] private EffectsClearingMaskView _mask;
#if UNITY_ENABLE_CHECKS

        private readonly HashSet<Renderer> _warned = new();
#endif

        private Camera _camera;
        private Camera _effectsCamera;
        private RenderTexture _map;
        private Material _material;
        private MeshRenderer _quad;
        private Transform _quadTransform;
        private CameraCullingMask _cullingMask;
        private float _holdRemaining;
        private bool _held;
        private bool _rendering;
        private bool _composing;
        private bool _volumesFrozen;
        private bool _broken;
#if UNITY_ENABLE_CHECKS
        private float _scanIn;
        private int _heldFrames;
        private int _renderedFrames;
#endif

        internal RenderTexture MapTexture => _map;

        internal bool Hdr => _hdr;
#if UNITY_ENABLE_CHECKS

        /// <summary>Frames the map was held for: each one it rendered, before it waited for its layers.</summary>
        internal int HeldFrames => _heldFrames;

        /// <summary>Frames the effects camera rendered.</summary>
        internal int RenderedFrames => _renderedFrames;
#endif

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _cullingMask = CameraCullingMask.Of(_camera);

            if (_effectLayers.value == 0)
            {
                Log.Warn("EffectsMap", "effect layers mask is empty — nothing will clear.", this);
            }
        }

        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += BeforeRender;
#if UNITY_ENABLE_CHECKS
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += CountRender;
#endif
        }

        private void LateUpdate()
        {
            _held = _settings != null && _settings.Enabled && _mask != null && Held()
                    && _effectLayers.value != 0 && EnsureResources();
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
            var off = BalloonParty.Cheats.CheatState.PerfOff;
            _held &= (off & BalloonParty.Cheats.PerfOff.Lens) == 0
                     && (_hdr || (off & BalloonParty.Cheats.PerfOff.HudMap) == 0);
#endif
#if UNITY_ENABLE_CHECKS
            if (_held)
            {
                _heldFrames++;
            }

            WarnUntracked();
#endif

            Apply();
            MirrorProjection();
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= BeforeRender;
#if UNITY_ENABLE_CHECKS
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= CountRender;
#endif
            SetState(false, false);
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.Release();
                Destroy(_map);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        // On game time, the clock the effects play on, so a level-up freeze holds the map with them.
        private bool Held()
        {
            if (_mask.IsClearing)
            {
                _holdRemaining = _settings.MapHoldSeconds;
                return true;
            }

            _holdRemaining -= Time.deltaTime;
            return _holdRemaining > 0f;
        }

        // Something can come alive after this LateUpdate (a late tick launches a trail), so the map looks again
        // just before the frame renders.
        private void BeforeRender()
        {
            Apply();
            MirrorProjection();
        }

        // The map renders only while something is alive on its layers: on any other frame it would lay back
        // nothing, and the camera's own pass draws the same picture. The glint is the glass's, not the map's, so
        // the quad stays while a clearing is open, map rendered or not, and never flickers as effects come and go.
        private void Apply()
        {
            var rendering = _held && LayerPresence.Any(_effectLayers.value);
            if (rendering)
            {
                EnsureTarget();
            }

            SetState(rendering, rendering || (_held && _mask.IsClearing));
        }

        private void SetState(bool rendering, bool composing)
        {
            if (rendering != _rendering)
            {
                _rendering = rendering;
                ClaimLayers(rendering);
                if (_effectsCamera != null)
                {
                    _effectsCamera.enabled = rendering;
                    _volumesFrozen = _volumesFrozen || (rendering && CameraVolumes.TryFreeze(_effectsCamera));
                }

                // A map not rendered this frame holds an old one: the quad lays back only its glint.
                if (_material != null)
                {
                    _material.SetTexture(MapTexId, rendering ? _map : Texture2D.blackTexture);
                }
            }

            if (composing != _composing)
            {
                _composing = composing;
                if (_quad != null)
                {
                    _quad.enabled = composing;
                }
            }
        }

        private void ClaimLayers(bool claim)
        {
            // Torn down with the camera, the owner may already be gone.
            if (_cullingMask == null)
            {
                return;
            }

            if (claim)
            {
                _cullingMask.Claim(_effectLayers.value);
            }
            else
            {
                _cullingMask.Release(_effectLayers.value);
            }
        }

        // After every script has moved the camera for this frame (a cinematic zoom runs in LateUpdate too), so
        // the map and the quad never frame last frame's view.
        private void MirrorProjection()
        {
            if (!_composing)
            {
                return;
            }

            var source = _stackBase != null ? _stackBase : _camera;
            var height = source.orthographicSize * 2f;
            _effectsCamera.orthographicSize = source.orthographicSize;
            _quadTransform.localScale = new Vector3(height * source.aspect, height, 1f);
        }

        private bool EnsureResources()
        {
            if (_material != null)
            {
                return true;
            }

            if (_broken)
            {
                return false;
            }

            var shader = _settings.CompositeShader;

            // Editor-only fallback — device builds strip shaders only referenced by name.
            if (shader == null)
            {
                shader = Shader.Find("BalloonParty/Display/EffectsMapComposite");
            }

            var compositeLayer = LayerMask.NameToLayer(_compositeLayerName);
            if (shader == null || compositeLayer < 0)
            {
                Log.Warn("EffectsMap", "composite shader or layer missing — assign the shader on the " +
                                       "EffectsClearingSettings asset. Effects won't clear.", this);
                _broken = true;
                return false;
            }

            _material = new Material(shader);
            CreateEffectsCamera();

            var quad = QuadRendererBuilder.Build(
                "EffectsMapComposite", _material, compositeLayer, _sortingLayerName, _sortingOrder);
            _quadTransform = quad.transform;
            _quadTransform.SetParent(transform, false);
            _quadTransform.localPosition = new Vector3(0f, 0f, QuadDistance);
            _quad = quad.GetComponent<MeshRenderer>();
            _quad.enabled = false;

            return true;
        }

        private void CreateEffectsCamera()
        {
            var go = new GameObject("EffectsMapCamera");
            go.transform.SetParent(transform, false);

            _effectsCamera = go.AddComponent<Camera>();
            _effectsCamera.orthographic = true;
            _effectsCamera.nearClipPlane = _camera.nearClipPlane;
            _effectsCamera.farClipPlane = _camera.farClipPlane;
            _effectsCamera.cullingMask = _effectLayers.value;
            _effectsCamera.clearFlags = CameraClearFlags.SolidColor;

            // Transparent black: the map holds premultiplied effects, its alpha their coverage.
            _effectsCamera.backgroundColor = Color.clear;
            _effectsCamera.allowHDR = _hdr;
            _effectsCamera.allowMSAA = false;

            var stackDepth = _stackBase != null ? _stackBase.depth : _camera.depth;
            _effectsCamera.depth = stackDepth - 1f;
            _effectsCamera.enabled = false;

            // Runtime-created cameras carry no serialized URP data; GetUniversalAdditionalCameraData adds it.
            var cameraData = _effectsCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
        }

        private void EnsureTarget()
        {
            var downscale = Mathf.Max(1, _settings.MapDownscale);
            var width = Mathf.Max(1, Screen.width / downscale);
            var height = Mathf.Max(1, Screen.height / downscale);

            if (_map != null && _map.width == width && _map.height == height)
            {
                return;
            }

            if (_map != null)
            {
                _map.Release();
                Destroy(_map);
            }

            // URP's RenderGraph rejects a depthless camera output texture. Nothing samples the depth, so 16 bits.
            var format = _hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            _map = new RenderTexture(width, height, 16, format)
            {
                name = "EffectsMap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            _effectsCamera.targetTexture = _map;
            _material.SetTexture(MapTexId, _map);
        }
#if UNITY_ENABLE_CHECKS

        // A source neither pooled nor watched would draw straight into the frame, outside the glass, while the map
        // skips its frames. Looked for now and then while the map is held with its layers said to be empty.
        private void WarnUntracked()
        {
            if (!_held || LayerPresence.Any(_effectLayers.value))
            {
                return;
            }

            _scanIn -= Time.unscaledDeltaTime;
            if (_scanIn > 0f)
            {
                return;
            }

            _scanIn = UntrackedScanSeconds;
            var untracked = LayerPresence.FindUntracked(_effectLayers.value);
            if (untracked != null && _warned.Add(untracked))
            {
                Log.Warn("EffectsMap", $"'{untracked.name}' draws on the map's layers but isn't pooled or watched " +
                                       "(LayerPresence), so the map skips it and it shows outside the glass.",
                    untracked);
            }
        }

        private void CountRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (camera == _effectsCamera)
            {
                _renderedFrames++;
            }
        }
#endif
    }
}

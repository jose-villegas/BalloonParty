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
    ///     empty that is the same blend into the same frame, so it only runs while a clearing is open: otherwise
    ///     the effects draw straight into the frame, as they would without it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(EffectsClearingMaskView.ExecutionOrder + 1)]
    internal class EffectsMapCompositor : MonoBehaviour
    {
        private const float QuadDistance = 5f;

        private static readonly int MapTexId = Shader.PropertyToID("_EffectsMapTex");

        [Tooltip("The effects this camera draws, moved into the map while a clearing is open.")]
        [SerializeField] private LayerMask _effectLayers;

        [Tooltip("The base camera of this camera's stack. The effects camera follows its projection and renders " +
                 "just before it, so the map is ready the same frame.")]
        [SerializeField] private Camera _stackBase;

        [Tooltip("An HDR map (ARGBHalf) for a camera that post-processes, so bloom still sees values over 1; an " +
                 "8-bit map for one drawn after post.")]
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

        private Camera _camera;
        private Camera _effectsCamera;
        private RenderTexture _map;
        private Material _material;
        private MeshRenderer _quad;
        private Transform _quadTransform;
        private int _removedLayers;
        private bool _live;
        private bool _broken;

        internal RenderTexture MapTexture => _map;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (_effectLayers.value == 0)
            {
                Log.Warn("EffectsMap", "effect layers mask is empty — nothing will clear.", this);
            }
        }

        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += MirrorProjection;
        }

        private void LateUpdate()
        {
            var live = _settings != null && _settings.Enabled && _mask != null && _mask.IsClearing
                       && _effectLayers.value != 0 && EnsureResources();

            if (live)
            {
                EnsureTarget();
            }

            SetLive(live);
            MirrorProjection();
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= MirrorProjection;
            SetLive(false);
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

        private void SetLive(bool live)
        {
            if (live == _live)
            {
                return;
            }

            _live = live;

            SuppressEffectLayers(live);

            if (_effectsCamera != null)
            {
                _effectsCamera.enabled = live;
            }

            if (_quad != null)
            {
                _quad.enabled = live;
            }
        }

        // The one place this writes its camera's culling mask. Fine on the HUD camera, which nothing else writes; the
        // main camera's mask also belongs to NavigationCameraReveal, so phase 3 needs one owner for it (the plan).
        private void SuppressEffectLayers(bool suppress)
        {
            // Torn down with the camera, the component may already be gone.
            if (_camera == null)
            {
                return;
            }

            // Only the bits this took out go back, so a mask someone else changes meanwhile is left alone.
            if (suppress)
            {
                _removedLayers = _camera.cullingMask & _effectLayers.value;
                _camera.cullingMask &= ~_effectLayers.value;
            }
            else
            {
                _camera.cullingMask |= _removedLayers;
                _removedLayers = 0;
            }
        }

        // After every script has moved the camera for this frame (a cinematic zoom runs in LateUpdate too), so
        // the map and the quad never frame last frame's view.
        private void MirrorProjection()
        {
            if (!_live)
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
    }
}

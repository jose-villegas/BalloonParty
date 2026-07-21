using System;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.GameState;
using BalloonParty.Slots.Actor;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     Owns the shared cloud field: bakes one scrolling cloud-density map into a camera-sized RT once
    ///     per frame and publishes it — plus its world bounds — as global shader properties, so every
    ///     consumer reads the SAME map with a single tap (the BackgroundCloud backdrop, sprite drop-shadows,
    ///     the GI/light smear, the wall net). A plain-C# DI singleton exactly like its siblings
    ///     <c>DisturbanceFieldService</c> / <c>SceneLightFieldService</c>: bounds come from
    ///     <see cref="IGameDisplayConfiguration" /> (entry-point-independent — no camera, so the Launcher's
    ///     additive-load dual-camera boot can't confuse it), the cloud roll is tuned on the blit material
    ///     (<see cref="IBackgroundFieldSettings.DensityMaterial" />), and there is no scene GameObject to place or
    ///     duplicate. GPU side: <c>Shaders/BalloonParty/Include/BackgroundField.cginc</c> (consumers) +
    ///     <c>BackgroundFieldGen.cginc</c> and the <c>BackgroundFieldDensity</c> blit (generation).
    /// </summary>
    internal sealed class BackgroundFieldService : IStartable, ITickable, IDisposable
    {
        private static readonly int DensityTexId = Shader.PropertyToID("_BackgroundDensityTex");
        private static readonly int BoundsMinId = Shader.PropertyToID("_BackgroundFieldBoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_BackgroundFieldBoundsSize");
        private static readonly int ActiveId = Shader.PropertyToID("_BackgroundFieldActive");
        private static readonly int WorldOffsetId = Shader.PropertyToID("_BackgroundWorldOffset");

        private readonly IBackgroundFieldSettings _settings;
        private readonly IGameDisplayConfiguration _display;
        private readonly ScenarioContentRoot _scenarioRoot;

        private RenderTexture _densityRT;
        private Rect _bounds;
        private Vector2 _ascendOffset;

        public BackgroundFieldService(
            IBackgroundFieldSettings settings,
            IGameDisplayConfiguration display,
            ScenarioContentRoot scenarioRoot)
        {
            _settings = settings;
            _display = display;
            _scenarioRoot = scenarioRoot;
        }

        void IStartable.Start()
        {
            if (_settings?.DensityMaterial == null || _display == null)
            {
                Log.Warn("BackgroundField", "disabled: assign a density material on the BackgroundFieldSettings.");
                return;
            }

            // Origin-centered ortho viewport, same as the disturbance / scene-light fields — reuse their
            // coordinate helper so the cloud field aligns with them and needs no camera.
            var coords = new DisturbanceFieldCoordinates(_display, _settings.TexelsPerUnit);
            _bounds = coords.Bounds;
            CreateRenderTexture(coords.Width, coords.Height);

            PushBoundsGlobals();
            Shader.SetGlobalTexture(DensityTexId, _densityRT);
            Shader.SetGlobalFloat(ActiveId, 1f);
            Bake();
        }

        void ITickable.Tick()
        {
            if (_densityRT == null)
            {
                return;
            }

            UpdateAscend();
            PushTransitionOffset();
            Bake();
        }

        void IDisposable.Dispose()
        {
            Shader.SetGlobalFloat(ActiveId, 0f);

            if (_densityRT != null)
            {
                _densityRT.Release();
                UnityEngine.Object.Destroy(_densityRT);
                _densityRT = null;
            }
        }

        // Renders the scrolling cloud density into the RT. The source texture is unused — the blit material
        // computes each texel from its world position (bounds globals) and the built-in clock.
        private void Bake()
        {
            if (_densityRT != null && _settings.DensityMaterial != null)
            {
                Graphics.Blit(Texture2D.whiteTexture, _densityRT, _settings.DensityMaterial);
            }
        }

        // Reflects the launch ascend (LaunchPlayTrigger.Begin ... await ... TransitionTo(Game)): roll the
        // clouds by LaunchAscend.Scroll over its Duration, eased out, then HOLD — so the game (which starts
        // only after the trigger's wait) continues at exactly the scroll it ended on. Unscaled, matching
        // the trigger's unscaled wait.
        private void UpdateAscend()
        {
            if (!LaunchAscend.IsActive)
            {
                return;
            }

            var p = Mathf.Clamp01((Time.unscaledTime - LaunchAscend.StartTime) / LaunchAscend.Duration);
            var eased = 1f - (1f - p) * (1f - p);
            _ascendOffset = LaunchAscend.Scroll * eased;
        }

        // Scrolls the cloud noise by the scenario root's transition displacement (the Ascent / restart
        // descent move that transform) plus the launch ascend, so the clouds react to those beats.
        private void PushTransitionOffset()
        {
            var scenario = _scenarioRoot?.Transform != null
                ? (Vector2)_scenarioRoot.Transform.position * _settings.TransitionParallax
                : Vector2.zero;
            Shader.SetGlobalVector(WorldOffsetId, scenario + _ascendOffset);
        }

        private void PushBoundsGlobals()
        {
            Shader.SetGlobalVector(BoundsMinId, new Vector4(_bounds.xMin, _bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(BoundsSizeId, new Vector4(_bounds.width, _bounds.height, 0f, 0f));
        }

        private void CreateRenderTexture(int width, int height)
        {
            width = Mathf.Max(4, width);
            height = Mathf.Max(4, height);
            // RG: R = density (shape), G = smooth intensity (see BackgroundFieldGenerate).
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RG16)
                ? RenderTextureFormat.RG16
                : RenderTextureFormat.ARGB32;

            _densityRT = new RenderTexture(width, height, 0, format)
            {
                name = "BackgroundDensity",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _densityRT.Create();
        }
    }
}

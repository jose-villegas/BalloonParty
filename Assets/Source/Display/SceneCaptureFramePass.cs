using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Draws the scene capture inside the main camera's frame, so no second camera goes through the pipeline for
    ///     it. The capture camera stays as the capture's description (its culling mask, view, projection, target and
    ///     clear colour) and never renders: <see cref="SceneCaptureService" /> culls with its parameters, and this
    ///     pass draws what that cull found into its target with its camera properties, as the camera would.
    /// </summary>
    internal sealed class SceneCaptureFramePass : ScriptableRenderPass
    {
        // The passes the 2D renderer draws sprites with (its DrawRenderer2DPass).
        private static readonly List<ShaderTagId> SpritePasses = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("Universal2D")
        };

        private readonly Camera _captureCamera;
        private CullingResults _culling;
        private RenderTexture _texture;
        private RTHandle _target;

        internal SceneCaptureFramePass(Camera captureCamera)
        {
            _captureCamera = captureCamera;
            profilingSampler = new ProfilingSampler("SceneCapture");

            // First thing in the main camera's frame: the main camera then sets up its own camera properties over
            // the capture camera's, so nothing is left to restore.
            renderPassEvent = RenderPassEvent.BeforeRendering;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            var drawing = RenderingUtils.CreateDrawingSettings(SpritePasses, renderingData, cameraData, lightData,
                SortingCriteria.CommonTransparent);

            // The 2D renderer's sort: its transparency sort mode is Default, which for an orthographic camera is
            // Orthographic. It draws a sorting layer range per pass; with no 2D lights its passes draw one after
            // another, which is this single list sorted by sorting layer first.
            drawing.sortingSettings = new SortingSettings(_captureCamera)
            {
                criteria = SortingCriteria.CommonTransparent,
                distanceMetric = DistanceMetric.Orthographic
            };

            // The capture RT carries a depth buffer for the camera path, and the graph won't import a texture
            // with both formats: it imports the colour alone, and the pass gets a transient depth of its own.
            var colour = new RenderTargetInfo
            {
                width = _texture.width,
                height = _texture.height,
                volumeDepth = 1,
                msaaSamples = 1,
                format = _texture.graphicsFormat
            };
            var depth = new TextureDesc(_texture.width, _texture.height)
            {
                format = GraphicsFormatUtility.GetDepthStencilFormat(24, 8),
                name = "SceneCaptureDepth"
            };

            using var builder = renderGraph.AddUnsafePass<PassData>(passName, out var data, profilingSampler);
            data.Camera = _captureCamera;
            data.Background = _captureCamera.backgroundColor;
            data.Target = renderGraph.ImportTexture(_target, colour);
            data.Depth = renderGraph.CreateTexture(depth);
            data.Renderers = renderGraph.CreateRendererList(
                new RendererListParams(_culling, drawing, FilteringSettings.defaultValue));

            builder.UseTexture(data.Target, AccessFlags.Write);
            builder.UseTexture(data.Depth, AccessFlags.Write);
            builder.UseRendererList(data.Renderers);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (PassData pass, UnsafeGraphContext context) => Execute(pass, context.cmd));
        }

        /// <summary>Readies the pass for one frame: what the capture camera's cull found, and where it goes.</summary>
        internal void Setup(CullingResults culling, RenderTexture target)
        {
            _culling = culling;
            if (_texture != target)
            {
                _texture = target;
                _target?.Release();
                _target = RTHandles.Alloc(new RenderTargetIdentifier(target.colorBuffer));
            }
        }

        internal void Dispose()
        {
            _target?.Release();
            _target = null;
            _texture = null;
        }

        private static void Execute(PassData pass, UnsafeCommandBuffer cmd)
        {
            cmd.SetRenderTarget(pass.Target, pass.Depth);
            cmd.SetupCameraProperties(pass.Camera);
            cmd.ClearRenderTarget(true, true, pass.Background);
            cmd.DrawRendererList(pass.Renderers);
        }

        private sealed class PassData
        {
            internal Camera Camera;
            internal Color Background;
            internal TextureHandle Target;
            internal TextureHandle Depth;
            internal RendererListHandle Renderers;
        }
    }
}

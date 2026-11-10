using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The water's surface simulation, published as the global <c>_WaterRippleTex</c> (r = surface height,
    ///     g/b = the water's flow across each texel's right and top edges, a = foam) with its texel size in
    ///     <c>_WaterRippleTexel</c>, which <c>BalloonParty/Scenario/Water</c> reads. A shallow-water solver over the
    ///     disturbance field's bounds (<c>Hidden/BalloonParty/Scenario/WaterRipple</c>): height and flow trade back and
    ///     forth so waves travel, surface tension sends short ripples ahead of long ones, the screen's edges reflect,
    ///     crests foam where they fold over sharply, and the foam rides along with them. Each impact the game reports
    ///     presses the water for a moment and drags it along the impact's heading, so a ball leaves a wake. Fixed
    ///     steps on game time: the same at any frame rate, held by a pause, slowed by the pop slow-mo. Only while
    ///     water shows.
    /// </summary>
    internal sealed class WaterRippleBuffer : IDisposable
    {
        private const float StepSeconds = 1f / 120f;
        // A long frame runs at most this many steps; the rest of its time is dropped rather than caught up on.
        private const int MaxStepsPerFrame = 4;
        // How long an impact presses the water, eased in and out: long enough that the push is smooth, so it starts
        // a wave rather than a spike of grid-sized chop.
        private const float PressSeconds = 0.12f;
        // Past this many impacts pressing at once the oldest gives way.
        private const int MaxPresses = 64;
        // The splat quad's half-size, in impact radii: the shader's Gaussian is gone well inside it.
        private const float SplatExtent = 2.5f;
        private const int SplatPass = 0;
        private const int StepPass = 1;

        private static readonly int FieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int ForceTexId = Shader.PropertyToID("_ForceTex");
        private static readonly int StepSecondsId = Shader.PropertyToID("_StepSeconds");
        private static readonly int TexelsPerUnitId = Shader.PropertyToID("_TexelsPerUnit");
        private static readonly int PressRadiusId = Shader.PropertyToID("_PressRadius");
        private static readonly int WaterRippleTexId = Shader.PropertyToID("_WaterRippleTex");
        private static readonly int WaterRippleTexelId = Shader.PropertyToID("_WaterRippleTexel");

        private readonly Material _material;
        private readonly float _texelsPerUnit;
        private readonly RenderTexture[] _state = new RenderTexture[2];
        private readonly List<Press> _presses = new();

        private RenderTexture _force;
        private int _read;
        private float _pendingSeconds;

        internal WaterRippleBuffer(Material rippleMaterial, float texelsPerUnit)
        {
            _texelsPerUnit = Mathf.Max(1f, texelsPerUnit);

            // A runtime copy, so play mode never writes the per-step uniforms into the material asset.
            _material = rippleMaterial != null && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? new Material(rippleMaterial) { hideFlags = HideFlags.HideAndDontSave }
                : null;

            if (_material == null)
            {
                Log.Warn("Water", "no ripples (no ripple material or no half-float targets): the surface only swells.");
            }

            Rest();
        }

        public void Dispose()
        {
            Rest();
            DestroyTargets();
            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        /// <summary>Starts from calm water, so nothing struck before the water appeared moves it.</summary>
        internal void Restart()
        {
            _presses.Clear();
            _pendingSeconds = 0f;
            Clear(_state[0]);
            Clear(_state[1]);
            Rest();
        }

        internal void Tick(float deltaTime, IReadOnlyList<ImpactEventBus.Impact> impacts)
        {
            if (_material == null)
            {
                return;
            }

            Vector2 fieldMin = Shader.GetGlobalVector(FieldBoundsMinId);
            Vector2 fieldSize = Shader.GetGlobalVector(FieldBoundsSizeId);
            if (fieldSize.x <= 0f || fieldSize.y <= 0f)
            {
                return;
            }

            var width = Mathf.Max(4, Mathf.RoundToInt(fieldSize.x * _texelsPerUnit));
            var height = Mathf.Max(4, Mathf.RoundToInt(fieldSize.y * _texelsPerUnit));
            EnsureTargets(width, height);

            deltaTime = Mathf.Max(0f, deltaTime);
            AgePresses(deltaTime);
            AddPresses(impacts, fieldMin, fieldSize);

            _pendingSeconds = Mathf.Min(_pendingSeconds + deltaTime, StepSeconds * MaxStepsPerFrame);
            var steps = Mathf.FloorToInt(_pendingSeconds / StepSeconds);
            _pendingSeconds -= steps * StepSeconds;
            if (steps > 0)
            {
                Splat(fieldMin, fieldSize);
                _material.SetTexture(ForceTexId, _force);
                _material.SetFloat(StepSecondsId, StepSeconds);
                _material.SetFloat(TexelsPerUnitId, _texelsPerUnit);
                for (var i = 0; i < steps; i++)
                {
                    Graphics.Blit(_state[_read], _state[1 - _read], _material, StepPass);
                    _read = 1 - _read;
                }
            }

            Shader.SetGlobalTexture(WaterRippleTexId, _state[_read]);
            Shader.SetGlobalVector(WaterRippleTexelId, new Vector4(1f / width, 1f / height, 0f, 0f));
        }

        private void AgePresses(float deltaTime)
        {
            for (var i = _presses.Count - 1; i >= 0; i--)
            {
                var press = _presses[i];
                press.Age += deltaTime;
                if (press.Age >= PressSeconds)
                {
                    _presses.RemoveAt(i);
                }
                else
                {
                    _presses[i] = press;
                }
            }
        }

        // Off-field impacts (past the camera) press nothing.
        private void AddPresses(IReadOnlyList<ImpactEventBus.Impact> impacts, Vector2 fieldMin, Vector2 fieldSize)
        {
            var fieldMax = fieldMin + fieldSize;
            var radiusScale = _material.GetFloat(PressRadiusId);
            var minRadius = 1.5f / _texelsPerUnit;
            for (var i = 0; i < impacts.Count; i++)
            {
                var impact = impacts[i];
                Vector2 origin = impact.Position;
                if (origin.x < fieldMin.x || origin.x > fieldMax.x || origin.y < fieldMin.y || origin.y > fieldMax.y)
                {
                    continue;
                }

                var strength = Mathf.Clamp01(impact.Radius * 1.5f);
                if (strength <= 0f)
                {
                    continue;
                }

                if (_presses.Count >= MaxPresses)
                {
                    _presses.RemoveAt(0);
                }
                var direction = impact.Direction.sqrMagnitude > 1e-6f ? impact.Direction.normalized : Vector2.zero;
                _presses.Add(new Press(
                    origin, Mathf.Max(impact.Radius * radiusScale, minRadius), strength, direction));
            }
        }

        // Draws every press into the force target, one additive quad each, at the strength its ease is at now.
        private void Splat(Vector2 fieldMin, Vector2 fieldSize)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _force;
            GL.Clear(false, true, Color.clear);
            if (_presses.Count > 0)
            {
                _material.SetPass(SplatPass);
                GL.PushMatrix();
                GL.LoadOrtho();
                GL.Begin(GL.QUADS);
                foreach (var press in _presses)
                {
                    var ease = Mathf.Sin(Mathf.PI * press.Age / PressSeconds);
                    var center = (press.Origin - fieldMin) / fieldSize;
                    var half = new Vector2(press.Radius / fieldSize.x, press.Radius / fieldSize.y) * SplatExtent;
                    GL.MultiTexCoord(1, new Vector3(press.Strength * ease, press.Direction.x, press.Direction.y));
                    Corner(center, half, -1f, -1f);
                    Corner(center, half, -1f, 1f);
                    Corner(center, half, 1f, 1f);
                    Corner(center, half, 1f, -1f);
                }

                GL.End();
                GL.PopMatrix();
            }

            RenderTexture.active = previous;
        }

        private static void Corner(Vector2 center, Vector2 half, float x, float y)
        {
            GL.MultiTexCoord2(0, x * SplatExtent, y * SplatExtent);
            GL.Vertex3(center.x + x * half.x, center.y + y * half.y, 0f);
        }

        // Black is calm water: flat, still, no foam.
        private void Rest()
        {
            Shader.SetGlobalTexture(WaterRippleTexId, Texture2D.blackTexture);
            Shader.SetGlobalVector(WaterRippleTexelId, new Vector4(0.25f, 0.25f, 0f, 0f));
        }

        private void EnsureTargets(int width, int height)
        {
            if (_force != null && _force.width == width && _force.height == height)
            {
                return;
            }

            DestroyTargets();
            _state[0] = CreateTarget(width, height);
            _state[1] = CreateTarget(width, height);
            _force = CreateTarget(width, height);
            Clear(_state[0]);
            Clear(_state[1]);
            _read = 0;
        }

        private void DestroyTargets()
        {
            Destroy(ref _state[0]);
            Destroy(ref _state[1]);
            Destroy(ref _force);
        }

        // Bilinear, so the surface shader reads a smooth field between texels; the solver samples texel centres,
        // where bilinear returns the texel itself.
        private static RenderTexture CreateTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            return target;
        }

        private static void Clear(RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        private static void Destroy(ref RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            target.Release();
            Object.Destroy(target);
            target = null;
        }

        private struct Press
        {
            public readonly Vector2 Origin;
            public readonly float Radius;
            public readonly float Strength;
            public readonly Vector2 Direction;
            public float Age;

            public Press(Vector2 origin, float radius, float strength, Vector2 direction)
            {
                Origin = origin;
                Radius = radius;
                Strength = strength;
                Direction = direction;
                Age = 0f;
            }
        }
    }
}

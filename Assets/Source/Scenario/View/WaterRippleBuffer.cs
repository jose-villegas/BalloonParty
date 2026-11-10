using System;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The water's ripples, published as the global <c>_WaterRippleTex</c> (r = surface height) with its texel size
    ///     in <c>_WaterRippleTexel</c>, which <c>BalloonParty/Scenario/Water</c> reads its rings from. A wave equation
    ///     over a height field covering the disturbance field, in its UV space
    ///     (<c>Hidden/BalloonParty/Scenario/WaterRipple</c>): what the field gains presses the water down, and the dip
    ///     travels outward as rings that fade. Fixed steps, so the rings walk at the same speed at any frame rate, and
    ///     game time, so a pause holds them and the pop slow-mo slows them. Only while water shows.
    /// </summary>
    internal sealed class WaterRippleBuffer : IDisposable
    {
        private const float StepSeconds = 1f / 60f;
        // A long frame runs at most this many steps; the rest of the time is dropped rather than caught up on.
        private const int MaxStepsPerFrame = 4;

        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int PreviousFieldId = Shader.PropertyToID("_PreviousField");
        private static readonly int StepSecondsId = Shader.PropertyToID("_StepSeconds");
        private static readonly int TexelsPerUnitId = Shader.PropertyToID("_TexelsPerUnit");
        private static readonly int TakeDriveId = Shader.PropertyToID("_TakeDrive");
        private static readonly int WaterRippleTexId = Shader.PropertyToID("_WaterRippleTex");
        private static readonly int WaterRippleTexelId = Shader.PropertyToID("_WaterRippleTexel");

        private readonly Material _material;
        private readonly float _texelsPerUnit;
        private readonly RenderTexture[] _state = new RenderTexture[2];

        private RenderTexture _previousField;
        private int _read;
        private bool _primed;
        private float _pendingSeconds;

        internal WaterRippleBuffer(Material rippleMaterial, float texelsPerUnit)
        {
            _texelsPerUnit = Mathf.Max(1f, texelsPerUnit);

            // A runtime copy, so play mode never writes the per-step uniforms into the material asset.
            _material = rippleMaterial != null
                        && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)
                        && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
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
            Release();
            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        /// <summary>Starts calm from the field as it is now, so a push the field is still holding doesn't land on the
        /// water all at once when it appears.</summary>
        internal void Restart()
        {
            _primed = false;
            Rest();
        }

        internal void Tick(float deltaTime)
        {
            if (_material == null || !(Shader.GetGlobalTexture(DisturbanceTexId) is RenderTexture field))
            {
                return;
            }

            Vector2 fieldSize = Shader.GetGlobalVector(FieldBoundsSizeId);
            var width = Mathf.Max(4, Mathf.RoundToInt(fieldSize.x * _texelsPerUnit));
            var height = Mathf.Max(4, Mathf.RoundToInt(fieldSize.y * _texelsPerUnit));

            if (EnsureTargets(width, height, field) || !_primed)
            {
                Clear(_state[0]);
                Clear(_state[1]);
                Graphics.Blit(field, _previousField);
                _pendingSeconds = 0f;
                _primed = true;
            }

            _pendingSeconds = Mathf.Min(_pendingSeconds + Mathf.Max(0f, deltaTime), StepSeconds * MaxStepsPerFrame);
            if (_pendingSeconds < StepSeconds)
            {
                // No step this frame: the field's gain waits for the next one rather than being lost.
                Publish();
                return;
            }

            _material.SetTexture(PreviousFieldId, _previousField);
            _material.SetFloat(StepSecondsId, StepSeconds);
            _material.SetFloat(TexelsPerUnitId, _texelsPerUnit);

            var takeDrive = 1f;
            while (_pendingSeconds >= StepSeconds)
            {
                _material.SetFloat(TakeDriveId, takeDrive);
                var write = 1 - _read;
                Graphics.Blit(_state[_read], _state[write], _material);
                _read = write;
                _pendingSeconds -= StepSeconds;
                takeDrive = 0f;
            }

            Graphics.Blit(field, _previousField);
            Publish();
        }

        private void Publish()
        {
            var state = _state[_read];
            Shader.SetGlobalTexture(WaterRippleTexId, state);
            Shader.SetGlobalVector(WaterRippleTexelId, new Vector4(1f / state.width, 1f / state.height, 0f, 0f));
        }

        // Black is a flat surface.
        private void Rest()
        {
            Shader.SetGlobalTexture(WaterRippleTexId, Texture2D.blackTexture);
            Shader.SetGlobalVector(WaterRippleTexelId, new Vector4(0.25f, 0.25f, 0f, 0f));
        }

        private bool EnsureTargets(int width, int height, RenderTexture field)
        {
            if (_state[0] != null && _state[0].width == width && _state[0].height == height
                && _previousField.width == field.width && _previousField.height == field.height)
            {
                return false;
            }

            Release();
            _previousField = Create(field.width, field.height, RenderTextureFormat.ARGBHalf);
            _state[0] = Create(width, height, RenderTextureFormat.RGHalf);
            _state[1] = Create(width, height, RenderTextureFormat.RGHalf);
            _read = 0;
            return true;
        }

        private void Release()
        {
            Destroy(ref _previousField);
            Destroy(ref _state[0]);
            Destroy(ref _state[1]);
        }

        private static RenderTexture Create(int width, int height, RenderTextureFormat format)
        {
            var target = new RenderTexture(width, height, 0, format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            return target;
        }

        private static void Clear(RenderTexture target)
        {
            var active = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
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
    }
}

using System;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The grass's own short memory of the disturbance field, published as the global <c>_GrassWakeTex</c> the
    ///     blades bend to. The shared field holds a push for seconds and spreads it (the clouds' slow drift); the
    ///     wake keeps only what the field gained each frame and lets it go on its own recovery
    ///     (<c>Hidden/BalloonParty/Scenario/GrassWake</c>), so the blades answer the ball at once and stand back up
    ///     quickly. Field-sized and in the field's UV space, so the blades sample both the same way. One small blit
    ///     and one copy a frame, only while the ground shows.
    /// </summary>
    internal sealed class GrassWakeBuffer : IDisposable
    {
        private static readonly int DisturbanceTexId = Shader.PropertyToID("_DisturbanceTex");
        private static readonly int PreviousFieldId = Shader.PropertyToID("_PreviousField");
        private static readonly int WakeRetainId = Shader.PropertyToID("_WakeRetain");
        private static readonly int GrassWakeTexId = Shader.PropertyToID("_GrassWakeTex");

        private readonly Material _material;
        private readonly RenderTexture[] _wake = new RenderTexture[2];

        private RenderTexture _previousField;
        private int _read;
        private bool _primed;

        internal GrassWakeBuffer(Material material)
        {
            // A runtime copy, so play mode never writes the per-frame uniforms into the material asset.
            _material = material != null && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? new Material(material) { hideFlags = HideFlags.HideAndDontSave }
                : null;

            if (_material == null)
            {
                Log.Warn("Ground", "no grass wake (no wake material or no half-float targets): the blades stay at rest.");
            }

            Rest();
        }

        public void Dispose()
        {
            Release();
            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        /// <summary>Starts over from the field as it is now, so a push it is still holding doesn't land on the
        /// blades all at once when the ground appears.</summary>
        internal void Restart()
        {
            _primed = false;
            Rest();
        }

        internal void Tick(float deltaTime, float recoverySeconds)
        {
            if (_material == null || !(Shader.GetGlobalTexture(DisturbanceTexId) is RenderTexture field))
            {
                return;
            }

            if (EnsureTargets(field.width, field.height) || !_primed)
            {
                Clear(_wake[0]);
                Clear(_wake[1]);
                Graphics.Blit(field, _previousField);
                _primed = true;
            }

            var write = 1 - _read;
            _material.SetTexture(PreviousFieldId, _previousField);
            _material.SetFloat(WakeRetainId, Mathf.Exp(-deltaTime / Mathf.Max(0.01f, recoverySeconds)));
            Graphics.Blit(_wake[_read], _wake[write], _material);
            _read = write;

            Graphics.Blit(field, _previousField);
            Shader.SetGlobalTexture(GrassWakeTexId, _wake[_read]);
        }

        // Black is no push and no stir: every blade at rest.
        private void Rest()
        {
            Shader.SetGlobalTexture(GrassWakeTexId, Texture2D.blackTexture);
        }

        private bool EnsureTargets(int width, int height)
        {
            if (_previousField != null && _previousField.width == width && _previousField.height == height)
            {
                return false;
            }

            Release();
            _previousField = Create(width, height);
            _wake[0] = Create(width, height);
            _wake[1] = Create(width, height);
            _read = 0;
            return true;
        }

        private void Release()
        {
            Destroy(ref _previousField);
            Destroy(ref _wake[0]);
            Destroy(ref _wake[1]);
        }

        // Half-float: the wake's push is signed, and the previous field copy keeps the field's own precision.
        private static RenderTexture Create(int width, int height)
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

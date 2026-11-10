using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The water's ripples, published as the global <c>_WaterRippleTex</c> (r = surface height) with its texel size
    ///     in <c>_WaterRippleTexel</c>, which <c>BalloonParty/Scenario/Water</c> reads its rings from. Each impact the
    ///     game reports starts a ring; one blit a frame of <c>Hidden/BalloonParty/Scenario/WaterRipple</c> draws every
    ///     ring in flight into a height field covering the disturbance field's bounds. Impacts close in place and time
    ///     to a ring already started join it, so a ball leaves a trail of rings rather than a smear. Ages run on game
    ///     time, so a pause holds the rings and the pop slow-mo slows them. Only while water shows.
    /// </summary>
    internal sealed class WaterRippleBuffer : IDisposable
    {
        // The shader's MAX_RINGS: past it the oldest ring gives way.
        private const int MaxRings = 48;
        // A ring older than this has faded to nothing (several of the material's fade time constants).
        private const float RingLifeSeconds = 3f;
        // An impact this close to a ring started this recently joins it.
        private const float JoinDistance = 0.45f;
        private const float JoinSeconds = 0.15f;

        private static readonly int FieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int RippleFieldMinId = Shader.PropertyToID("_RippleFieldMin");
        private static readonly int RippleFieldSizeId = Shader.PropertyToID("_RippleFieldSize");
        private static readonly int RingsId = Shader.PropertyToID("_Rings");
        private static readonly int RingCountId = Shader.PropertyToID("_RingCount");
        private static readonly int WaterRippleTexId = Shader.PropertyToID("_WaterRippleTex");
        private static readonly int WaterRippleTexelId = Shader.PropertyToID("_WaterRippleTexel");

        private readonly Material _material;
        private readonly float _texelsPerUnit;
        // xy = world origin, z = age in seconds, w = strength 0..1.
        private readonly Vector4[] _rings = new Vector4[MaxRings];

        private RenderTexture _target;
        private int _ringCount;

        internal WaterRippleBuffer(Material rippleMaterial, float texelsPerUnit)
        {
            _texelsPerUnit = Mathf.Max(1f, texelsPerUnit);

            // A runtime copy, so play mode never writes the per-frame uniforms into the material asset.
            _material = rippleMaterial != null && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf)
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
            Destroy(ref _target);
            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        /// <summary>Starts from calm water, so nothing struck before the water appeared rings on it.</summary>
        internal void Restart()
        {
            _ringCount = 0;
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

            Age(Mathf.Max(0f, deltaTime));
            StartRings(impacts, fieldMin, fieldSize);

            var width = Mathf.Max(4, Mathf.RoundToInt(fieldSize.x * _texelsPerUnit));
            var height = Mathf.Max(4, Mathf.RoundToInt(fieldSize.y * _texelsPerUnit));
            EnsureTarget(width, height);

            _material.SetVector(RippleFieldMinId, fieldMin);
            _material.SetVector(RippleFieldSizeId, fieldSize);
            _material.SetVectorArray(RingsId, _rings);
            _material.SetInt(RingCountId, _ringCount);
            Graphics.Blit(null, _target, _material);

            Shader.SetGlobalTexture(WaterRippleTexId, _target);
            Shader.SetGlobalVector(WaterRippleTexelId, new Vector4(1f / width, 1f / height, 0f, 0f));
        }

        // Ages every ring and drops the faded ones, keeping the rest packed at the front.
        private void Age(float deltaTime)
        {
            var kept = 0;
            for (var i = 0; i < _ringCount; i++)
            {
                var ring = _rings[i];
                ring.z += deltaTime;
                if (ring.z < RingLifeSeconds)
                {
                    _rings[kept++] = ring;
                }
            }

            _ringCount = kept;
        }

        // Off-field impacts (past the camera) start no ring.
        private void StartRings(IReadOnlyList<ImpactEventBus.Impact> impacts, Vector2 fieldMin, Vector2 fieldSize)
        {
            var fieldMax = fieldMin + fieldSize;
            for (var i = 0; i < impacts.Count; i++)
            {
                var impact = impacts[i];
                Vector2 origin = impact.Position;
                if (origin.x < fieldMin.x || origin.x > fieldMax.x || origin.y < fieldMin.y || origin.y > fieldMax.y)
                {
                    continue;
                }

                var strength = Mathf.Clamp01(impact.Radius * 1.5f);
                if (strength <= 0f || TryJoin(origin, strength))
                {
                    continue;
                }

                _rings[_ringCount < MaxRings ? _ringCount++ : Oldest()] = new Vector4(origin.x, origin.y, 0f, strength);
            }
        }

        private bool TryJoin(Vector2 origin, float strength)
        {
            for (var i = 0; i < _ringCount; i++)
            {
                var ring = _rings[i];
                if (ring.z <= JoinSeconds && ((Vector2)ring - origin).sqrMagnitude <= JoinDistance * JoinDistance)
                {
                    ring.w = Mathf.Max(ring.w, strength);
                    _rings[i] = ring;
                    return true;
                }
            }

            return false;
        }

        private int Oldest()
        {
            var oldest = 0;
            for (var i = 1; i < _ringCount; i++)
            {
                if (_rings[i].z > _rings[oldest].z)
                {
                    oldest = i;
                }
            }

            return oldest;
        }

        // Black is a flat surface.
        private void Rest()
        {
            Shader.SetGlobalTexture(WaterRippleTexId, Texture2D.blackTexture);
            Shader.SetGlobalVector(WaterRippleTexelId, new Vector4(0.25f, 0.25f, 0f, 0f));
        }

        private void EnsureTarget(int width, int height)
        {
            if (_target != null && _target.width == width && _target.height == height)
            {
                return;
            }

            Destroy(ref _target);
            _target = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _target.Create();
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

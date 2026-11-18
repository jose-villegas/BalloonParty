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
    ///     presses the water for a moment and drags it along the impact's heading, so a ball leaves a wake. An
    ///     impact that carries a blast (a bomb, the nuke) also digs a crater (<see cref="WaterCrater" />): its water
    ///     is thrown out into a rim, which travels off as a wave while the rest floods back in. A blast that runs a
    ///     line (each stretch a piercing shot flies) digs a furrow along it, so a lance leaves a wake. Fixed steps on game
    ///     time: the same at any frame rate, held by a pause, slowed by the pop slow-mo. Only while water shows.
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
        // Craters being dug at once (the step shader's array): past it the oldest stops where it is. The fastest
        // lance (93 world units a second) has about eight stretches of its wake being dug at a time.
        private const int MaxBlasts = 12;
        // The step shader's variant that moves the craters' water: on only while one is being dug.
        private const string BlastsKeyword = "BLASTS_ON";
        private const int SplatPass = 0;
        private const int StepPass = 1;

        private static readonly int FieldBoundsMinId = Shader.PropertyToID("_FieldBoundsMin");
        private static readonly int FieldBoundsSizeId = Shader.PropertyToID("_FieldBoundsSize");
        private static readonly int ForceTexId = Shader.PropertyToID("_ForceTex");
        private static readonly int StepSecondsId = Shader.PropertyToID("_StepSeconds");
        private static readonly int TexelsPerUnitId = Shader.PropertyToID("_TexelsPerUnit");
        private static readonly int PressRadiusId = Shader.PropertyToID("_PressRadius");
        private static readonly int BlastRadiusId = Shader.PropertyToID("_BlastRadius");
        private static readonly int BlastDepthId = Shader.PropertyToID("_BlastDepth");
        private static readonly int BlastsId = Shader.PropertyToID("_Blasts");
        private static readonly int BlastCountId = Shader.PropertyToID("_BlastCount");
        private static readonly int WaterRippleTexId = Shader.PropertyToID("_WaterRippleTex");
        private static readonly int WaterRippleTexelId = Shader.PropertyToID("_WaterRippleTexel");

        private readonly Material _material;
        private readonly float _texelsPerUnit;
        private readonly RenderTexture[] _state = new RenderTexture[2];
        private readonly List<Press> _presses = new();
        private readonly List<Blast> _blasts = new();
        private readonly Vector4[] _blastUniforms = new Vector4[MaxBlasts];
        private readonly Dig[] _digs = new Dig[MaxBlasts];

        private RenderTexture _force;
        private int _read;
        private float _pendingSeconds;
        private bool _blasting;
        private int _digCount;

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
            _blasts.Clear();
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
            AgeBlasts(deltaTime);
            AddPresses(impacts, fieldMin, fieldSize);

            _pendingSeconds = Mathf.Min(_pendingSeconds + deltaTime, StepSeconds * MaxStepsPerFrame);
            var steps = Mathf.FloorToInt(_pendingSeconds / StepSeconds);
            _pendingSeconds -= steps * StepSeconds;
            if (steps > 0)
            {
                Splat(fieldMin, fieldSize);
                var digging = BeginDigs();
                _material.SetTexture(ForceTexId, _force);
                _material.SetFloat(StepSecondsId, StepSeconds);
                _material.SetFloat(TexelsPerUnitId, _texelsPerUnit);
                for (var i = 0; i < steps; i++)
                {
                    if (digging)
                    {
                        DigStep(i, steps, fieldMin, fieldSize, new Vector2(width, height));
                    }

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

        private void AgeBlasts(float deltaTime)
        {
            for (var i = 0; i < _blasts.Count; i++)
            {
                var blast = _blasts[i];
                blast.Age += deltaTime;
                _blasts[i] = blast;
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

                if (impact.Blast.IsBlast)
                {
                    AddBlast(origin, impact.Blast);
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

        private void AddBlast(Vector2 origin, ImpactEventBus.Blast blast)
        {
            var crater = WaterCrater.From(
                blast, _material.GetFloat(BlastRadiusId), _material.GetFloat(BlastDepthId), StepSeconds);
            if (crater.Radius <= 0f || crater.Depth <= 0f)
            {
                return;
            }

            // The stretches of one straight flight reported this frame (several fixed steps in a long frame) are
            // dug as one furrow, so a hitch can't flood the blasts and push stretches out, leaving gaps in a wake.
            for (var i = _blasts.Count - 1; i >= 0 && _blasts[i].Age <= 0f; i--)
            {
                if (WaterCrater.TryJoin(_blasts[i].Origin, _blasts[i].Crater, origin, crater, out var at, out var joined))
                {
                    _blasts[i] = new Blast(at, joined);
                    return;
                }
            }

            if (_blasts.Count >= MaxBlasts)
            {
                _blasts.RemoveAt(0);
            }

            _blasts.Add(new Blast(origin, crater));
        }

        // Notes how much of each crater this frame's steps dig: the share its dig has come on since the last steps
        // ran. So the whole of a dig is done at any frame rate, whichever frames ran steps. False with none to dig.
        private bool BeginDigs()
        {
            if (_blasts.Count == 0)
            {
                if (_blasting)
                {
                    _blasting = false;
                    _material.DisableKeyword(BlastsKeyword);
                }

                return false;
            }

            _digCount = _blasts.Count;
            for (var i = _digCount - 1; i >= 0; i--)
            {
                var blast = _blasts[i];
                var dug = blast.Crater.DugBy(blast.Age);
                _digs[i] = new Dig(blast, dug, _texelsPerUnit);
                blast.Dug = dug;
                if (dug >= 1f)
                {
                    _blasts.RemoveAt(i);
                }
                else
                {
                    _blasts[i] = blast;
                }
            }

            _material.SetFloat(BlastCountId, _digCount);
            if (!_blasting)
            {
                _blasting = true;
                _material.EnableKeyword(BlastsKeyword);
            }

            return true;
        }

        // Hands the step shader each crater's dig for one of the frame's steps: an even share of the frame's, and
        // where the dig stands by then. A furrow's dig runs its line, so each step has its own place on it: handed
        // over once a frame, a furrow would be dug as a row of holes, one per frame.
        private void DigStep(int step, int steps, Vector2 fieldMin, Vector2 fieldSize, Vector2 texels)
        {
            for (var i = 0; i < MaxBlasts; i++)
            {
                _blastUniforms[i] = Vector4.zero;
            }

            for (var i = 0; i < _digCount; i++)
            {
                var dig = _digs[i];
                var share = (dig.To - dig.From) / steps;
                var at = dig.Origin + dig.Crater.OffsetAt(dig.From + (step + 0.5f) * share);
                var middle = (at - fieldMin) / fieldSize * texels;
                var spreadTexels = dig.Crater.Spread * _texelsPerUnit;
                _blastUniforms[i] = new Vector4(
                    middle.x, middle.y, 1f / (spreadTexels * spreadTexels), dig.Scale * share);
            }

            _material.SetVectorArray(BlastsId, _blastUniforms);
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

        private struct Blast
        {
            public readonly Vector2 Origin;
            public readonly WaterCrater Crater;
            public float Age;
            // The share of the crater the steps have dug so far.
            public float Dug;

            public Blast(Vector2 origin, WaterCrater crater)
            {
                Origin = origin;
                Crater = crater;
                Age = 0f;
                Dug = 0f;
            }
        }

        // One crater's dig over one frame's steps.
        private readonly struct Dig
        {
            public readonly Vector2 Origin;
            public readonly WaterCrater Crater;
            // The shares of the dig done before this frame's steps and after them.
            public readonly float From;
            public readonly float To;
            // What the whole dig scales the potential by.
            public readonly float Scale;

            public Dig(Blast blast, float dug, float texelsPerUnit)
            {
                Origin = blast.Origin;
                Crater = blast.Crater;
                From = blast.Dug;
                To = dug;
                Scale = blast.Crater.PotentialScale(texelsPerUnit);
            }
        }
    }
}

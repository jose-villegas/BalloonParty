using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Projectile.Clearance
{
    /// <summary>
    ///     Fixed-capacity circle slots, one per ball, keyed by the ball's reference. Each frame the owner
    ///     calls <see cref="Track" /> for every ball in flight, then <see cref="Advance" /> once: a tracked
    ///     slot follows its ball and eases in, an untracked one stays where its ball was last seen and eases
    ///     out, and a slot that reaches zero is freed.
    /// </summary>
    internal sealed class ClearanceField : IClearanceField
    {
        private readonly IClearanceSettings _settings;
        private readonly object[] _keys = new object[ClearanceCircle.MaxCircles];
        private readonly Vector2[] _centers = new Vector2[ClearanceCircle.MaxCircles];
        private readonly float[] _rawStrengths = new float[ClearanceCircle.MaxCircles];
        private readonly bool[] _live = new bool[ClearanceCircle.MaxCircles];
        private readonly bool[] _tracked = new bool[ClearanceCircle.MaxCircles];
        private readonly ClearanceCircle[] _circles = new ClearanceCircle[ClearanceCircle.MaxCircles];
        private int _count;

        public int Count => _count;

        internal ClearanceField(IClearanceSettings settings)
        {
            _settings = settings;
        }

        public ClearanceCircle CircleAt(int index)
        {
            return _circles[index];
        }

        /// <summary>A ball in flight this frame at <paramref name="center" />.</summary>
        internal void Track(object key, Vector2 center)
        {
            var slot = FindLive(key);
            if (slot < 0)
            {
                slot = Allocate(key);
                if (slot < 0)
                {
                    Log.Warn(nameof(ClearanceField),
                        $"All {ClearanceCircle.MaxCircles} slots hold live balls; a ball goes without a circle.");
                    return;
                }
            }

            _centers[slot] = center;
            _tracked[slot] = true;
        }

        internal void Advance(float deltaTime)
        {
            var easeIn = _settings.FadeIn > 0f ? deltaTime / _settings.FadeIn : 1f;
            var easeOut = _settings.FadeOut > 0f ? deltaTime / _settings.FadeOut : 1f;
            _count = 0;

            for (var i = 0; i < ClearanceCircle.MaxCircles; i++)
            {
                if (!_live[i] && _rawStrengths[i] <= 0f)
                {
                    continue;
                }

                if (_tracked[i])
                {
                    _rawStrengths[i] = Mathf.Min(1f, _rawStrengths[i] + easeIn);
                }
                else
                {
                    _live[i] = false;
                    _rawStrengths[i] = Mathf.Max(0f, _rawStrengths[i] - easeOut);
                }

                _tracked[i] = false;

                if (_rawStrengths[i] <= 0f && !_live[i])
                {
                    _keys[i] = null;
                    continue;
                }

                var strength = Mathf.SmoothStep(0f, 1f, _rawStrengths[i]);
                _circles[_count++] = new ClearanceCircle(_centers[i], _settings.Radius * strength, strength);
            }
        }

        private int FindLive(object key)
        {
            for (var i = 0; i < ClearanceCircle.MaxCircles; i++)
            {
                if (_live[i] && ReferenceEquals(_keys[i], key))
                {
                    return i;
                }
            }

            return -1;
        }

        // A key seen again after its circle stopped being tracked is a fresh flight of a reused model, not
        // the old one coming back: it gets a new slot, so the old circle keeps fading where it was instead
        // of jumping to the new ball.
        private int Allocate(object key)
        {
            var slot = -1;
            var weakest = float.MaxValue;
            for (var i = 0; i < ClearanceCircle.MaxCircles; i++)
            {
                if (_live[i])
                {
                    continue;
                }

                if (ReferenceEquals(_keys[i], key))
                {
                    _keys[i] = null;
                }

                if (_rawStrengths[i] < weakest)
                {
                    weakest = _rawStrengths[i];
                    slot = i;
                }
            }

            if (slot < 0)
            {
                return -1;
            }

            _keys[slot] = key;
            _live[slot] = true;
            _rawStrengths[slot] = 0f;
            return slot;
        }
    }
}

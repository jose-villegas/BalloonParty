using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Turns the balls in flight into the clearing mask's stamps: one capsule per ball, from where it was last
    ///     frame to where it is now, in mask UV (<c>xy</c> the start, <c>zw</c> the end). A capsule rather than a
    ///     disc so a fast ball leaves an unbroken corridor. A ball not stamped last frame (just fired, or back in
    ///     flight after a gap) stamps a disc, and so does every ball while the clearing has no trail. Each stamp's
    ///     radius grows with its ball's shields.
    /// </summary>
    internal sealed class EffectsClearingStamper
    {
        /// <summary>The mask shader's stamp array size.</summary>
        internal const int MaxStamps = 8;

        private readonly IEffectsClearingSettings _settings;

        // A handful of balls at most, so linear scans beat a keyed structure and allocate nothing.
        private readonly List<Track> _tracks = new();
        private readonly List<Candidate> _candidates = new();

        private int _frame;

        internal EffectsClearingStamper(IEffectsClearingSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Writes this frame's stamps, and each one's radius in view heights, and returns how many. Past
        /// <see cref="MaxStamps" /> balls, the ones nearest the view's centre win.</summary>
        internal int Fill(IProjectileRegistry registry, in ClearingView view, Vector4[] stamps, float[] radii)
        {
            _frame++;
            _candidates.Clear();
            var trail = _settings.Trail;

            for (var i = 0; i < registry.InFlightCount; i++)
            {
                var model = registry.InFlightAt(i);
                Vector2 position = registry.InFlightPositionAt(i);
                var from = position;

                var index = IndexOf(model);
                if (index < 0)
                {
                    _tracks.Add(new Track(model, position, _frame));
                }
                else
                {
                    if (trail && _tracks[index].Frame == _frame - 1)
                    {
                        from = _tracks[index].Position;
                    }

                    _tracks[index] = new Track(model, position, _frame);
                }

                var start = view.ToUv(from);
                var end = view.ToUv(position);
                var offset = new Vector2((end.x - 0.5f) * view.Aspect, end.y - 0.5f);
                var radius = view.ToUvHeight(RadiusOf(model));
                _candidates.Add(new Candidate(new Vector4(start.x, start.y, end.x, end.y), radius, offset.sqrMagnitude));
            }

            var count = Mathf.Min(_candidates.Count, Mathf.Min(MaxStamps, Mathf.Min(stamps.Length, radii.Length)));
            if (_candidates.Count > count)
            {
                _candidates.Sort();
            }

            for (var i = 0; i < count; i++)
            {
                stamps[i] = _candidates[i].Stamp;
                radii[i] = _candidates[i].Radius;
            }

            return count;
        }

        /// <summary>Drops a ball's last position; called when the registry retires it.</summary>
        internal void Forget(IProjectileModel model)
        {
            var index = IndexOf(model);
            if (index >= 0)
            {
                _tracks.RemoveAt(index);
            }
        }

        // A ball on its last leg has no shields left but still needs to be seen, so it clears as one.
        private float RadiusOf(IProjectileModel model)
        {
            var growth = (model.ShieldsRemaining.Value - 1f) / (_settings.ShieldsForMaxRadius - 1f);
            return Mathf.Lerp(_settings.MinClearRadius, _settings.MaxClearRadius, growth);
        }

        private int IndexOf(IProjectileModel model)
        {
            for (var i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].Model == model)
                {
                    return i;
                }
            }

            return -1;
        }

        private readonly struct Track
        {
            internal readonly IProjectileModel Model;
            internal readonly Vector2 Position;
            internal readonly int Frame;

            internal Track(IProjectileModel model, Vector2 position, int frame)
            {
                Model = model;
                Position = position;
                Frame = frame;
            }
        }

        private readonly struct Candidate : IComparable<Candidate>
        {
            internal readonly Vector4 Stamp;
            internal readonly float Radius;
            private readonly float _distanceFromCenter;

            internal Candidate(Vector4 stamp, float radius, float distanceFromCenter)
            {
                Stamp = stamp;
                Radius = radius;
                _distanceFromCenter = distanceFromCenter;
            }

            public int CompareTo(Candidate other)
            {
                return _distanceFromCenter.CompareTo(other._distanceFromCenter);
            }
        }
    }
}

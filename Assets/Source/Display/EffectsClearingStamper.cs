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
    ///     radius grows with its ball's shields. Without a trail the mask holds only this frame's stamps, so a ball
    ///     that leaves flight (retired, or its last glide over) leaves a disc where it was last seen that fades over
    ///     <see cref="IEffectsClearingSettings.LingerSeconds" />, its dome shrinking inward to nothing.
    /// </summary>
    internal sealed class EffectsClearingStamper
    {
        /// <summary>The mask shader's stamp array size.</summary>
        internal const int MaxStamps = 8;

        private readonly IEffectsClearingSettings _settings;

        // A handful of balls at most, so linear scans beat a keyed structure and allocate nothing.
        private readonly List<Track> _tracks = new();
        private readonly List<Fade> _fades = new();
        private readonly List<Candidate> _candidates = new();

        private int _frame;

        internal EffectsClearingStamper(IEffectsClearingSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Writes this frame's stamps into <paramref name="stamps" /> and returns how many. Past
        /// <see cref="MaxStamps" />, the ones nearest the view's centre win. <paramref name="deltaTime" /> is how far
        /// the fading discs of balls gone from flight move on.</summary>
        internal int Fill(IProjectileRegistry registry, in ClearingView view, float deltaTime,
            EffectsClearingStamps stamps)
        {
            _frame++;
            _candidates.Clear();
            var trail = _settings.Trail;
            AdvanceFades(view, deltaTime);

            for (var i = 0; i < registry.InFlightCount; i++)
            {
                var model = registry.InFlightAt(i);
                Vector2 position = registry.InFlightPositionAt(i);
                var from = position;

                var radius = RadiusOf(model);
                var index = IndexOf(model);
                if (index < 0)
                {
                    _tracks.Add(new Track(model, position, radius, _frame));
                }
                else
                {
                    if (trail && _tracks[index].Frame == _frame - 1)
                    {
                        from = _tracks[index].Position;
                    }

                    _tracks[index] = new Track(model, position, radius, _frame);
                }

                AddCandidate(view, from, position, radius, 0f);
            }

            if (!trail)
            {
                FadeTracksGoneFromFlight(view);
            }

            var count = Mathf.Min(_candidates.Count, MaxStamps);
            if (_candidates.Count > count)
            {
                _candidates.Sort();
            }

            for (var i = 0; i < count; i++)
            {
                stamps.Capsules[i] = _candidates[i].Stamp;
                stamps.Radii[i] = _candidates[i].Radius;
                stamps.Fades[i] = _candidates[i].Fade;
            }

            return count;
        }

        /// <summary>Drops a ball's last position; called when the registry retires it. Without a trail, its disc
        /// starts to fade where the ball was last seen.</summary>
        internal void Forget(IProjectileModel model)
        {
            var index = IndexOf(model);
            if (index < 0)
            {
                return;
            }

            if (!_settings.Trail)
            {
                _fades.Add(new Fade(_tracks[index].Position, _tracks[index].Radius, 0f));
            }

            _tracks.RemoveAt(index);
        }

        private void AdvanceFades(in ClearingView view, float deltaTime)
        {
            var linger = Mathf.Max(_settings.LingerSeconds, 1e-4f);
            for (var i = _fades.Count - 1; i >= 0; i--)
            {
                var elapsed = _fades[i].Elapsed + deltaTime;
                if (elapsed >= linger)
                {
                    _fades.RemoveAt(i);
                    continue;
                }

                _fades[i] = new Fade(_fades[i].Position, _fades[i].Radius, elapsed);
                AddCandidate(view, _fades[i].Position, _fades[i].Position, _fades[i].Radius, elapsed / linger);
            }
        }

        // A ball the registry no longer has in flight but never retired (its last glide over) fades from where it was
        // last seen, at full dome this frame, so the shrink starts without a gap.
        private void FadeTracksGoneFromFlight(in ClearingView view)
        {
            for (var i = _tracks.Count - 1; i >= 0; i--)
            {
                if (_tracks[i].Frame == _frame)
                {
                    continue;
                }

                _fades.Add(new Fade(_tracks[i].Position, _tracks[i].Radius, 0f));
                AddCandidate(view, _tracks[i].Position, _tracks[i].Position, _tracks[i].Radius, 0f);
                _tracks.RemoveAt(i);
            }
        }

        private void AddCandidate(in ClearingView view, Vector2 from, Vector2 to, float radius, float fade)
        {
            var start = view.ToUv(from);
            var end = view.ToUv(to);
            var offset = new Vector2((end.x - 0.5f) * view.Aspect, end.y - 0.5f);
            _candidates.Add(new Candidate(new Vector4(start.x, start.y, end.x, end.y), view.ToUvHeight(radius), fade,
                offset.sqrMagnitude));
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
            internal readonly float Radius;
            internal readonly int Frame;

            internal Track(IProjectileModel model, Vector2 position, float radius, int frame)
            {
                Model = model;
                Position = position;
                Radius = radius;
                Frame = frame;
            }
        }

        private readonly struct Fade
        {
            internal readonly Vector2 Position;
            internal readonly float Radius;
            internal readonly float Elapsed;

            internal Fade(Vector2 position, float radius, float elapsed)
            {
                Position = position;
                Radius = radius;
                Elapsed = elapsed;
            }
        }

        private readonly struct Candidate : IComparable<Candidate>
        {
            internal readonly Vector4 Stamp;
            internal readonly float Radius;
            internal readonly float Fade;
            private readonly float _distanceFromCenter;

            internal Candidate(Vector4 stamp, float radius, float fade, float distanceFromCenter)
            {
                Stamp = stamp;
                Radius = radius;
                Fade = fade;
                _distanceFromCenter = distanceFromCenter;
            }

            public int CompareTo(Candidate other)
            {
                return _distanceFromCenter.CompareTo(other._distanceFromCenter);
            }
        }
    }
}

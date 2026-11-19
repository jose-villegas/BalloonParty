using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Which rings the formation glow draws (<c>FormationGlow</c> shader), how brightly, and how far each has
    ///     dissolved. A ring is connected while its member is in a formation with a fellow member of that formation
    ///     within <see cref="IFormationGlowSettings.FadeDistance" />: its brightness follows how close the nearest
    ///     fellow is, full at <see cref="IFormationGlowSettings.FullDistance" />. Rings join into one outline only
    ///     with the rings of their own formation; two formations standing side by side keep an outline each.
    ///     Disconnected for any reason (out of formation,
    ///     left alone, popped, cleared with the board), the ring stays where its member was last and dissolves over
    ///     <see cref="IFormationGlowSettings.DissolveSeconds" /> at the brightness it had, instead of vanishing. A
    ///     ring connecting dissolves in the same way, over the same time.
    /// </summary>
    internal sealed class FormationGlowField
    {
        /// <summary>The shader's fixed array size (<c>MAX_POINTS</c>).</summary>
        internal const int MaxPoints = 24;

        private readonly List<Ring> _rings = new();
        private readonly Stack<Ring> _spare = new();
        private readonly List<Vector2> _connected = new();
        private readonly List<int> _connectedFormations = new();

        /// <summary>The field's value at a lone member's ring, so its outline sits at the ring radius.</summary>
        internal static float Threshold(float ringRadius, float reach)
        {
            var t = 1f - ringRadius * ringRadius / (reach * reach);
            return t * t * t;
        }

        /// <summary>
        ///     How far apart two whole members' rings stop joining into one outline: where their two fields, summed
        ///     halfway between them, fall to the threshold.
        /// </summary>
        internal static float JoinDistance(float ringRadius, float reach)
        {
            var half = Mathf.Pow(Threshold(ringRadius, reach) * 0.5f, 1f / 3f);
            return 2f * reach * Mathf.Sqrt(Mathf.Max(0f, 1f - half));
        }

        /// <summary>
        ///     How much of its field a ring adds at <paramref name="presence" />: all of it when whole, and at none
        ///     no more than the threshold, so a lone ring shrinks into its member as it dissolves.
        /// </summary>
        internal static float Shape(float presence, float threshold)
        {
            return threshold + (1f - threshold) * presence;
        }

        /// <summary>
        ///     The field the shader sums at <paramref name="point" /> for one formation: the rings whose entry in
        ///     <paramref name="formations" /> is <paramref name="formation" />, or every ring when no formations are
        ///     given. The view doesn't need it; it pins the shader's sum, so the tests can tell where rings join.
        /// </summary>
        internal static float FieldAt(
            IReadOnlyList<Vector4> points, int count, Vector2 point, float reach, float threshold,
            IReadOnlyList<float> formations = null, float formation = 0f)
        {
            var reach2 = reach * reach;
            var field = 0f;
            for (var i = 0; i < count; i++)
            {
                if (formations != null && formations[i] != formation)
                {
                    continue;
                }

                var r2 = ((Vector2)points[i] - point).sqrMagnitude;
                if (r2 < reach2)
                {
                    var t = 1f - r2 / reach2;
                    field += Shape(points[i].w, threshold) * t * t * t;
                }
            }

            return field;
        }

        /// <summary>
        ///     Moves the rings on by <paramref name="deltaTime" /> and writes the ones to draw into
        ///     <paramref name="points" /> (xy centre, z brightness, w presence: 1 whole, falling to 0 as it dissolves)
        ///     and each one's formation into <paramref name="formations" />, returning how many;
        ///     <paramref name="bounds" /> covers their light. The rings of one formation are written next to each
        ///     other, which is how the shader tells where one formation's outline ends. Past
        ///     <see cref="MaxPoints" />, the rest are left out.
        /// </summary>
        internal int Step(
            IReadOnlyList<IFormationGlowSource> sources, IFormationGlowSettings settings, float deltaTime,
            Vector4[] points, float[] formations, out Rect bounds)
        {
            Match(sources, settings);
            Advance(deltaTime / settings.DissolveSeconds);
            return Write(points, formations, settings, out bounds);
        }

        // Pairs each source with its ring, opening one for a source that has just connected, and sets what each
        // ring is heading for: its closeness while connected, nothing once not.
        private void Match(IReadOnlyList<IFormationGlowSource> sources, IFormationGlowSettings settings)
        {
            _connected.Clear();
            _connectedFormations.Clear();
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i].GlowsInFormation)
                {
                    _connected.Add(sources[i].GlowCentre);
                    _connectedFormations.Add(sources[i].GlowFormation);
                }
            }

            for (var i = 0; i < _rings.Count; i++)
            {
                _rings[i].Seen = false;
            }

            var index = 0;
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var target = source.GlowsInFormation ? Closeness(index++, settings) : 0f;
                var ring = Find(source);
                if (ring == null)
                {
                    if (target <= 0f)
                    {
                        continue;
                    }

                    ring = Rent(source, target);
                }

                ring.Seen = true;
                ring.Centre = source.GlowCentre;
                ring.Target = target;

                // Kept once the member has left or gone, so its ring dissolves with its fellows' and not into a
                // neighbouring formation's.
                ring.Formation = source.GlowFormation;
            }
        }

        private void Advance(float rate)
        {
            for (var i = _rings.Count - 1; i >= 0; i--)
            {
                var ring = _rings[i];
                if (!ring.Seen)
                {
                    // Its balloon has gone: the ring stays where it was last and dissolves there.
                    ring.Source = null;
                    ring.Target = 0f;
                }

                if (ring.Target > 0f)
                {
                    ring.Brightness = Mathf.MoveTowards(ring.Brightness, ring.Target, rate);
                    ring.Presence = Mathf.MoveTowards(ring.Presence, 1f, rate);
                }
                else
                {
                    // The brightness holds, so the ring dissolves as it was rather than dimming first.
                    ring.Presence = Mathf.MoveTowards(ring.Presence, 0f, rate);
                }

                if (ring.Presence <= 0f && ring.Target <= 0f)
                {
                    _rings[i] = _rings[_rings.Count - 1];
                    _rings.RemoveAt(_rings.Count - 1);
                    ring.Source = null;
                    _spare.Push(ring);
                }
            }
        }

        private int Write(Vector4[] points, float[] formations, IFormationGlowSettings settings, out Rect bounds)
        {
            var count = 0;
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            for (var i = 0; i < _rings.Count && count < points.Length; i++)
            {
                var ring = _rings[i];
                if (ring.Presence <= 0f)
                {
                    continue;
                }

                // Slid in after the last ring of its own formation written so far: a handful of rings, and each
                // formation's stay together.
                var at = count++;
                while (at > 0 && formations[at - 1] > ring.Formation)
                {
                    points[at] = points[at - 1];
                    formations[at] = formations[at - 1];
                    at--;
                }

                points[at] = new Vector4(ring.Centre.x, ring.Centre.y, ring.Brightness, ring.Presence);
                formations[at] = ring.Formation;
                min = Vector2.Min(min, ring.Centre);
                max = Vector2.Max(max, ring.Centre);
            }

            if (count == 0)
            {
                bounds = default;
                return 0;
            }

            // The ring's light trails a little past the field's reach.
            var margin = settings.Reach + settings.RingWidth * 3f;
            bounds = Rect.MinMaxRect(min.x - margin, min.y - margin, max.x + margin, max.y + margin);
            return count;
        }

        // How close the nearest fellow member of its own formation is to the one at index, as a weight: 0 when
        // alone.
        private float Closeness(int index, IFormationGlowSettings settings)
        {
            var nearest = float.PositiveInfinity;
            var centre = _connected[index];
            var formation = _connectedFormations[index];
            for (var j = 0; j < _connected.Count; j++)
            {
                if (j != index && _connectedFormations[j] == formation)
                {
                    nearest = Mathf.Min(nearest, (_connected[j] - centre).magnitude);
                }
            }

            if (float.IsPositiveInfinity(nearest))
            {
                return 0f;
            }

            var t = Mathf.Clamp01((settings.FadeDistance - nearest) / (settings.FadeDistance - settings.FullDistance));
            return t * t * (3f - 2f * t);
        }

        private Ring Find(IFormationGlowSource source)
        {
            for (var i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Source == source)
                {
                    return _rings[i];
                }
            }

            return null;
        }

        // A new ring starts at its full brightness and only its presence grows, so it dissolves in as a leaving
        // one dissolves out, rather than fading up.
        private Ring Rent(IFormationGlowSource source, float target)
        {
            var ring = _spare.Count > 0 ? _spare.Pop() : new Ring();
            ring.Source = source;
            ring.Brightness = target;
            ring.Presence = 0f;
            _rings.Add(ring);
            return ring;
        }

        private sealed class Ring
        {
            internal IFormationGlowSource Source;
            internal Vector2 Centre;
            internal int Formation;
            internal float Target;
            internal float Brightness;
            internal float Presence;
            internal bool Seen;
        }
    }
}

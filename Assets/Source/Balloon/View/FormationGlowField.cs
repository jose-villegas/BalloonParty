using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Which formation members light the formation glow, and how brightly (<c>FormationGlow</c> shader). A
    ///     member's weight is how far into its formation it is times how close its nearest fellow is: full at
    ///     <see cref="IFormationGlowSettings.FullDistance" />, none past <see cref="IFormationGlowSettings.FadeDistance" />.
    /// </summary>
    internal static class FormationGlowField
    {
        /// <summary>The shader's fixed array size (<c>MAX_POINTS</c>).</summary>
        internal const int MaxPoints = 24;

        /// <summary>The field's value at a lone member's ring, so its outline sits at the ring radius.</summary>
        internal static float Threshold(float ringRadius, float reach)
        {
            var t = 1f - ringRadius * ringRadius / (reach * reach);
            return t * t * t;
        }

        /// <summary>
        ///     The field the shader sums at <paramref name="point" />. The view doesn't need it; it pins the shader's
        ///     sum, so the tests can tell where rings join.
        /// </summary>
        internal static float FieldAt(IReadOnlyList<Vector4> points, int count, Vector2 point, float reach)
        {
            var reach2 = reach * reach;
            var field = 0f;
            for (var i = 0; i < count; i++)
            {
                var r2 = ((Vector2)points[i] - point).sqrMagnitude;
                if (r2 < reach2)
                {
                    var t = 1f - r2 / reach2;
                    field += t * t * t;
                }
            }

            return field;
        }

        /// <summary>
        ///     Writes the glowing members into <paramref name="points" /> (xy their centre, z their weight) and
        ///     returns how many; <paramref name="bounds" /> covers their light. Past <see cref="MaxPoints" /> members
        ///     in formation, the rest are left out.
        /// </summary>
        internal static int Fill(
            IReadOnlyList<IFormationGlowSource> sources, IFormationGlowSettings settings, Vector4[] points,
            out Rect bounds)
        {
            var candidates = 0;
            for (var i = 0; i < sources.Count && candidates < points.Length; i++)
            {
                var membership = sources[i].GlowMembership;
                if (membership > 0f)
                {
                    var centre = sources[i].GlowCentre;
                    points[candidates++] = new Vector4(centre.x, centre.y, membership, 0f);
                }
            }

            // Weights read every candidate's position, so they're worked out before any is moved down the array.
            for (var i = 0; i < candidates; i++)
            {
                points[i].w = points[i].z * Closeness(points, candidates, i, settings);
            }

            var count = 0;
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            for (var i = 0; i < candidates; i++)
            {
                var weight = points[i].w;
                if (weight <= 0f)
                {
                    continue;
                }

                var centre = (Vector2)points[i];
                points[count++] = new Vector4(centre.x, centre.y, weight, 0f);
                min = Vector2.Min(min, centre);
                max = Vector2.Max(max, centre);
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

        private static float Closeness(Vector4[] points, int count, int index, IFormationGlowSettings settings)
        {
            var nearest = float.PositiveInfinity;
            var centre = (Vector2)points[index];
            for (var j = 0; j < count; j++)
            {
                if (j != index)
                {
                    nearest = Mathf.Min(nearest, ((Vector2)points[j] - centre).magnitude);
                }
            }

            if (float.IsPositiveInfinity(nearest))
            {
                return 0f;
            }

            var t = Mathf.Clamp01((settings.FadeDistance - nearest) / (settings.FadeDistance - settings.FullDistance));
            return t * t * (3f - 2f * t);
        }
    }
}

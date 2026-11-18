using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>The light one group of joined formation rings casts: a beam from one end of the group to the other.</summary>
    internal readonly struct FormationGlowBeam
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;

        /// <summary>How brightly the group's rings glow on average, 0 to 1.</summary>
        public readonly float Level;

        public FormationGlowBeam(Vector2 start, Vector2 end, float level)
        {
            Start = start;
            End = end;
            Level = level;
        }
    }

    /// <summary>
    ///     Turns the formation glow's rings into the lights they cast on the scene (<c>FormationGlowView</c>, through
    ///     the scene-light field). A light per ring would spend the field's few lights on one long formation, so the
    ///     rings that stand close enough to join are one group, and a group casts one beam: a capsule from its two
    ///     members furthest apart, which is exact for a line or a diagonal and close for a ring round an item or a
    ///     bent snake.
    /// </summary>
    internal static class FormationGlowBeams
    {
        /// <summary>The most beams the glow casts; groups past it cast none.</summary>
        internal const int MaxBeams = 8;

        // A group dimmer than this casts nothing worth a light.
        private const float MinLevel = 0.02f;

        /// <summary>
        ///     Groups the first <paramref name="count" /> of <paramref name="points" /> (xy centre, z brightness, w
        ///     presence, as <c>FormationGlowField.Step</c> writes them) and writes one beam per group into
        ///     <paramref name="beams" />, returning how many. Two rings within <paramref name="joinDistance" /> of each
        ///     other are in one group, and so is any chain of them. <paramref name="groups" /> is scratch, at least
        ///     <paramref name="count" /> long.
        /// </summary>
        internal static int Group(
            Vector4[] points, int count, float joinDistance, int[] groups, FormationGlowBeam[] beams)
        {
            for (var i = 0; i < count; i++)
            {
                groups[i] = i;
            }

            // Each ring takes the lowest group among the rings it joins, until nothing changes: a chain of
            // rings ends up with its first member's index, however it winds.
            while (JoinOnce(points, count, joinDistance * joinDistance, groups))
            {
            }

            var written = 0;
            for (var group = 0; group < count && written < beams.Length && written < MaxBeams; group++)
            {
                if (groups[group] != group)
                {
                    continue;
                }

                if (TryBeam(points, count, groups, group, out var beam))
                {
                    beams[written++] = beam;
                }
            }

            return written;
        }

        private static bool JoinOnce(Vector4[] points, int count, float squaredJoin, int[] groups)
        {
            var changed = false;
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    if (groups[i] == groups[j] || SquaredDistance(points[i], points[j]) > squaredJoin)
                    {
                        continue;
                    }

                    var lowest = Mathf.Min(groups[i], groups[j]);
                    groups[i] = lowest;
                    groups[j] = lowest;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool TryBeam(Vector4[] points, int count, int[] groups, int group, out FormationGlowBeam beam)
        {
            var level = 0f;
            var members = 0;
            var start = (Vector2)points[group];
            var end = start;
            var furthest = -1f;
            for (var i = 0; i < count; i++)
            {
                if (groups[i] != group)
                {
                    continue;
                }

                level += points[i].z * points[i].w;
                members++;
                for (var j = i; j < count; j++)
                {
                    if (groups[j] != group)
                    {
                        continue;
                    }

                    var apart = SquaredDistance(points[i], points[j]);
                    if (apart > furthest)
                    {
                        furthest = apart;
                        start = points[i];
                        end = points[j];
                    }
                }
            }

            level = members > 0 ? level / members : 0f;
            beam = new FormationGlowBeam(start, end, Mathf.Clamp01(level));
            return level >= MinLevel;
        }

        private static float SquaredDistance(Vector4 a, Vector4 b)
        {
            var x = a.x - b.x;
            var y = a.y - b.y;
            return (x * x) + (y * y);
        }
    }
}

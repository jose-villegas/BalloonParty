using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Formation;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The rings over the balloons item covers stand round (<see cref="ICoveredHosts" />). Each covered balloon is
    ///     a ring source of the formation covering it, so that formation's outline closes over it: without one the
    ///     covered balloon is a hole in the middle of a full cover, and a notch in an open one. The covered balloon's
    ///     own view knows nothing of it. <see cref="FormationGlowField" /> knows a ring by its source, so a covered
    ///     balloon keeps one source for as long as the same cover stands round it, and
    ///     <see cref="Sync" /> changes nothing, and allocates nothing, while the covers stand as they did. A balloon
    ///     no longer covered loses its source, and the field dissolves its ring where it was.
    /// </summary>
    internal sealed class FormationGlowHosts
    {
        private readonly List<HostRing> _rings = new();
        private readonly Stack<HostRing> _spare = new();

        /// <summary>
        ///     Brings the sources in <paramref name="sources" /> in line with <paramref name="hosts" />: one for each
        ///     covered balloon, added when its cover is first seen and taken out once it is gone.
        /// </summary>
        internal void Sync(IReadOnlyList<CoveredHost> hosts, FormationGlowSources sources)
        {
            for (var i = 0; i < _rings.Count; i++)
            {
                _rings[i].Covered = false;
            }

            for (var i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                var ring = Find(host.Actor, host.FormationId) ?? Add(host, sources);
                ring.Covered = true;
                ring.Centre = host.Centre;
            }

            for (var i = _rings.Count - 1; i >= 0; i--)
            {
                var ring = _rings[i];
                if (ring.Covered)
                {
                    continue;
                }

                _rings.RemoveAt(i);
                ring.Registration.Dispose();
                ring.Registration = null;
                ring.Actor = null;
                _spare.Push(ring);
            }
        }

        // The same balloon under another formation is another ring: the first dissolves as its own formation's.
        private HostRing Find(ISlotActor actor, int formation)
        {
            for (var i = 0; i < _rings.Count; i++)
            {
                if (ReferenceEquals(_rings[i].Actor, actor) && _rings[i].Formation == formation)
                {
                    return _rings[i];
                }
            }

            return null;
        }

        // The registration, not the pooled ring, is what the field knows the ring by, so a ring taken up again for
        // another balloon never takes over the one it left dissolving.
        private HostRing Add(CoveredHost host, FormationGlowSources sources)
        {
            var ring = _spare.Count > 0 ? _spare.Pop() : new HostRing();
            ring.Actor = host.Actor;
            ring.Formation = host.FormationId;
            ring.Centre = host.Centre;
            ring.Registration = sources.Add(ring);
            _rings.Add(ring);
            return ring;
        }

        private sealed class HostRing : IFormationGlowSource
        {
            internal ISlotActor Actor;
            internal int Formation;
            internal Vector2 Centre;
            internal bool Covered;
            internal IDisposable Registration;

            public Vector2 GlowCentre => Centre;

            // Listed only while covered: it lights, as a member's does, by how close the cover's members have drawn.
            public bool GlowsInFormation => true;
            public int GlowFormation => Formation;
        }
    }
}

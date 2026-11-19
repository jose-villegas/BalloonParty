using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>A balloon that can light the formation glow: an unbreakable's formation look.</summary>
    internal interface IFormationGlowSource
    {
        /// <summary>World centre of the balloon.</summary>
        Vector2 GlowCentre { get; }

        /// <summary>Whether it is in a formation now, gliding to its slot or there.</summary>
        bool GlowsInFormation { get; }

        /// <summary>
        ///     Which formation it is in, or was last: rings join only with the rings of their own formation
        ///     (<c>IFormationMember.FormationId</c>).
        /// </summary>
        int GlowFormation { get; }
    }

    /// <summary>
    ///     The balloons that can light the formation glow, each in from its bind until its despawn. Each spawn is its
    ///     own entry, so the ring a despawned balloon leaves dissolving is never taken over by the same pooled look
    ///     spawning again.
    /// </summary>
    internal sealed class FormationGlowSources
    {
        private readonly List<IFormationGlowSource> _sources = new();

        internal IReadOnlyList<IFormationGlowSource> All => _sources;

        internal IDisposable Add(IFormationGlowSource source)
        {
            var registration = new Registration(source, _sources);
            _sources.Add(registration);
            return registration;
        }

        private sealed class Registration : IFormationGlowSource, IDisposable
        {
            private readonly IFormationGlowSource _source;
            private readonly List<IFormationGlowSource> _sources;

            public Vector2 GlowCentre => _source.GlowCentre;
            public bool GlowsInFormation => _source.GlowsInFormation;
            public int GlowFormation => _source.GlowFormation;

            internal Registration(IFormationGlowSource source, List<IFormationGlowSource> sources)
            {
                _source = source;
                _sources = sources;
            }

            public void Dispose()
            {
                _sources.Remove(this);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>A balloon that can light the formation glow: an unbreakable's formation look.</summary>
    internal interface IFormationGlowSource
    {
        /// <summary>World centre of the balloon.</summary>
        Vector2 GlowCentre { get; }

        /// <summary>How far into its formation it is: 0 out, 1 in, easing between as it joins and leaves.</summary>
        float GlowMembership { get; }
    }

    /// <summary>The balloons that can light the formation glow, each in from its bind until its despawn.</summary>
    internal sealed class FormationGlowSources
    {
        private readonly List<IFormationGlowSource> _sources = new();

        internal IReadOnlyList<IFormationGlowSource> All => _sources;

        internal IDisposable Add(IFormationGlowSource source)
        {
            _sources.Add(source);
            return Disposable.Create(() => _sources.Remove(source));
        }
    }
}

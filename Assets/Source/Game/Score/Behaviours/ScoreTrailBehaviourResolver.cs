using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Messages;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Maps a score group to its choreography handler by group total: the highest-<c>MinPoints</c> entry
    ///     whose threshold the group clears wins. Shared by <c>ScoreTrailService</c> (which spawns) and
    ///     <c>LevelUpCinematic</c> (which derives the tipping id) so the id can never diverge from what the
    ///     handler actually registers.
    /// </summary>
    internal sealed class ScoreTrailBehaviourResolver
    {
        private readonly IScoreTrailBehaviour _fallback;
        private readonly Entry[] _entries;

        private bool _warnedEmpty;

        internal ScoreTrailBehaviourResolver(
            IScoreTrailBehaviourConfiguration config,
            IReadOnlyDictionary<ScoreTrailBehaviourId, IScoreTrailBehaviour> handlers)
        {
            _fallback = handlers[ScoreTrailBehaviourId.DefaultScore];
            _entries = BuildEntries(config, handlers);
        }

        internal IScoreTrailBehaviour Resolve(int points)
        {
            if (_entries.Length == 0)
            {
                WarnEmptyConfigOnce();
                return _fallback;
            }

            foreach (var entry in _entries)
            {
                if (points >= entry.MinPoints)
                {
                    return entry.Handler;
                }
            }

            // No entry cleared its threshold (a config with no zero floor) — the default is the safe carrier.
            return _fallback;
        }

        internal TrailId PrincipalIdFor(in ScorePointsGroupMessage msg)
        {
            return Resolve(msg.Points).GetPrincipalId(in msg);
        }

        // Descending by MinPoints so Resolve's first clear-threshold match is the most specific handler.
        private static Entry[] BuildEntries(
            IScoreTrailBehaviourConfiguration config,
            IReadOnlyDictionary<ScoreTrailBehaviourId, IScoreTrailBehaviour> handlers)
        {
            var source = config?.Entries;
            if (source == null || source.Count == 0)
            {
                return Array.Empty<Entry>();
            }

            var entries = new List<Entry>(source.Count);
            foreach (var entry in source)
            {
                if (handlers.TryGetValue(entry.Id, out var handler))
                {
                    entries.Add(new Entry(entry.MinPoints, handler));
                }
            }

            entries.Sort((a, b) => b.MinPoints.CompareTo(a.MinPoints));
            return entries.ToArray();
        }

        private void WarnEmptyConfigOnce()
        {
            if (_warnedEmpty)
            {
                return;
            }

            _warnedEmpty = true;
            Log.Warn("ScoreTrailResolver",
                "no ScoreTrailBehaviourConfiguration bound (or it has no " +
                "entries) — falling back to DefaultScore. Assign the asset on GameLifetimeScope to enable " +
                "score-magnitude discrimination.");
        }

        private readonly struct Entry
        {
            internal readonly int MinPoints;
            internal readonly IScoreTrailBehaviour Handler;

            internal Entry(int minPoints, IScoreTrailBehaviour handler)
            {
                MinPoints = minPoints;
                Handler = handler;
            }
        }
    }
}

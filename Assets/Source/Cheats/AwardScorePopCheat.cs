#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Awards one N-point pop of a chosen colour through the REAL scoring path (a popped stand-in
    ///     dispatched through the hit pipeline, like <see cref="ScoreCheatHelper.FillColor"/>), so the
    ///     award claims progress, publishes ONE N-point group, and drives the BigScore shape
    ///     decomposition — the way to summon any catalog shape on demand. The hit direction is
    ///     randomized per trigger so the tumble/bicone alignment gets exercised too.
    /// </summary>
    internal class AwardScorePopCheat : ICheat, ICheatControls
    {
        // One preset per catalog denomination; arbitrary totals (combo decompositions) go via the field.
        private static readonly int[] Presets = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 30, 50, 100 };

        private readonly IActiveLevelParameters _levelParams;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly ColorStreakTracker _streak;

        private int _points = 12;
        private int _colorIndex;

        public string Name => "Award Score Pop";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "shapes", "trails" };

        public AwardScorePopCheat(
            IActiveLevelParameters levelParams,
            IHitDispatcher hitDispatcher,
            ColorStreakTracker streak)
        {
            _levelParams = levelParams;
            _hitDispatcher = hitDispatcher;
            _streak = streak;
        }

        public void Execute()
        {
            var colors = _levelParams.Current.AllowedColors;
            if (colors == null || colors.Count == 0)
            {
                return;
            }

            var colorName = colors[_colorIndex % colors.Count];

            // Reset the streak first so the multiplier is 1 and the pop lands EXACTLY _points — the
            // whole point is summoning a specific decomposition (12 = the stellated dodecahedron, …).
            _streak.Reset();

            // ScoreValue N + HitsToPop 0 pops in one attribution of N points (see ScoreCheatHelper's
            // stand-in notes); the random in-plane direction exercises the shapes' hit-derived tumble.
            var fakeModel = new BalloonModel(new BalloonModelConfig(scoreValue: _points, hitsToPop: 0));
            fakeModel.Color.Value = colorName;

            var direction = Random.insideUnitCircle.normalized;
            _hitDispatcher.Dispatch(new ActorHitMessage(fakeModel,
                Vector3.zero,
                new Vector3(direction.x, direction.y, 0f),
                HitOutcome.Pop,
                new DamageContext(1, DamageFlags.Normal, colorName)));
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Points", GUILayout.Width(52));
            if (GUILayout.Button("−", GUILayout.Width(28)))
            {
                _points = Mathf.Max(1, _points - 1);
            }

            GUILayout.Label(_points.ToString(), GUILayout.Width(36));
            if (GUILayout.Button("+", GUILayout.Width(28)))
            {
                _points++;
            }

            var colors = _levelParams.Current.AllowedColors;
            var colorLabel = colors != null && colors.Count > 0
                ? colors[_colorIndex % colors.Count]
                : "—";
            if (GUILayout.Button(colorLabel, GUILayout.Width(72)))
            {
                _colorIndex++;
            }

            if (GUILayout.Button("Award"))
            {
                Execute();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (var preset in Presets)
            {
                if (GUILayout.Button(preset.ToString(), GUILayout.Width(34)))
                {
                    _points = preset;
                    Execute();
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}
#endif

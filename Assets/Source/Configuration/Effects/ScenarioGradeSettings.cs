using BalloonParty.Configuration.Level;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>
    ///     Authored colour grade for each scenario, so the sky, the meadow and the water read in the same light
    ///     (see <c>ScenarioGradeView</c>). Read every frame: tune it live in Play mode. Wire the asset into
    ///     GameLifetimeScope.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Scenario Grade Settings", fileName = "ScenarioGradeSettings")]
    internal sealed class ScenarioGradeSettings : ScriptableObject, IScenarioGradeSettings
    {
        [Tooltip("Off leaves the frame with only the base look of the default volume profile (ACES and its exposure).")]
        [SerializeField] private bool _enabled = true;

        [SerializeField] private ScenarioGrade _sky = ScenarioGrade.Neutral;
        [SerializeField] private ScenarioGrade _meadow = ScenarioGrade.Neutral;
        [SerializeField] private ScenarioGrade _water = ScenarioGrade.Neutral;

        public bool Enabled => _enabled;

        public ScenarioGrade For(ScenarioKind kind)
        {
            return kind switch
            {
                ScenarioKind.Ground => _meadow,
                ScenarioKind.Water => _water,
                _ => _sky
            };
        }
    }
}
